using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ChessApp.Backend.Data;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Services;

public interface ILichessIntegrationService
{
    LichessAuthorizeResponse StartAuthorization(Guid userId);
    LichessAuthorizeResponse StartSignIn();
    Task<LichessSignInResult> SignInAsync(string code, string state, CancellationToken ct = default);
    Task<LinkedAccountDto> CompleteAuthorizationAsync(Guid userId, string code, string state, CancellationToken ct = default);
    Task<LinkedAccountDto> RefreshAsync(Guid userId, CancellationToken ct = default);
    Task UnlinkAsync(Guid userId, CancellationToken ct = default);
    Task<int> SyncStaleAccountsAsync(TimeSpan maxAge, CancellationToken ct = default);
}

/// <summary>
/// Lichess account linking via OAuth 2 authorization code + PKCE.
/// The server owns the PKCE verifier and state; the app only opens the
/// authorize URL and hands back the code it receives on the deep link.
/// The access token is used once to read the account identity, then revoked.
/// </summary>
public class LichessIntegrationService : ILichessIntegrationService
{
    // Time controls shown in the app; variants (atomic, ...) are ignored
    private static readonly string[] TrackedCategories =
        { "ultraBullet", "bullet", "blitz", "rapid", "classical", "correspondence", "chess960" };

    // Candidates for the single headline rating (User.LichessRating) used in lists
    private static readonly string[] HeadlineCategories = { "classical", "rapid", "blitz", "bullet" };

    private static readonly TimeSpan PendingAuthLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ManualRefreshCooldown = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly ILichessClient _lichess;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LichessIntegrationService> _logger;

    public LichessIntegrationService(
        ApplicationDbContext dbContext,
        ILichessClient lichess,
        IMemoryCache cache,
        ILogger<LichessIntegrationService> logger)
    {
        _dbContext = dbContext;
        _lichess = lichess;
        _cache = cache;
        _logger = logger;
    }

    public LichessAuthorizeResponse StartAuthorization(Guid userId) => Start(userId, scope: null);

    public LichessAuthorizeResponse StartSignIn() => Start(userId: null, scope: "email:read");

    private LichessAuthorizeResponse Start(Guid? userId, string? scope)
    {
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        // In-memory: fine for a single API instance. Move to a distributed
        // cache (Redis) before running more than one.
        _cache.Set(CacheKey(state), new PendingAuth(userId, verifier), PendingAuthLifetime);

        return new LichessAuthorizeResponse
        {
            AuthorizeUrl = _lichess.BuildAuthorizeUrl(state, challenge, scope),
            State = state
        };
    }

    public async Task<LinkedAccountDto> CompleteAuthorizationAsync(
        Guid userId, string code, string state, CancellationToken ct = default)
    {
        var pending = TakePending(state);
        if (pending.UserId != userId)
            throw new LichessAuthException("This Lichess sign-in was started by a different user.");

        var (lichessUser, _) = await ReadIdentityAsync(code, pending.CodeVerifier, withEmail: false, ct);

        var externalId = lichessUser.Id.ToLowerInvariant();

        var takenBy = await _dbContext.LinkedAccounts
            .AnyAsync(a => a.Platform == LinkedPlatforms.Lichess
                        && a.ExternalId == externalId
                        && a.UserId != userId, ct);
        if (takenBy)
            throw new InvalidOperationException(
                $"Lichess account '{lichessUser.Username}' is already linked to another player.");

        var user = await _dbContext.Users
            .Include(u => u.LinkedAccounts)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found");

        var account = user.LinkedAccounts.FirstOrDefault(a => a.Platform == LinkedPlatforms.Lichess);
        if (account == null)
        {
            account = new LinkedAccount { UserId = userId, Platform = LinkedPlatforms.Lichess, IsVerified = true };
            _dbContext.LinkedAccounts.Add(account);
        }
        // Re-linking with a different Lichess account replaces the old one
        account.ConnectedAt = DateTime.UtcNow;

        Apply(account, user, lichessUser);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} linked Lichess account {LichessId}", userId, externalId);
        return LinkedAccountMapping.ToDto(account);
    }

    public async Task<LichessSignInResult> SignInAsync(string code, string state, CancellationToken ct = default)
    {
        var pending = TakePending(state);
        if (pending.UserId != null)
            throw new LichessAuthException("This Lichess sign-in was started for linking, not signing in.");

        var (lichessUser, email) = await ReadIdentityAsync(code, pending.CodeVerifier, withEmail: true, ct);
        if (lichessUser.Disabled)
            throw new LichessAuthException("This Lichess account is closed.");

        var externalId = lichessUser.Id.ToLowerInvariant();

        // Returning player: whoever has this Lichess account linked (signing up
        // with Lichess links it too, so this covers both ways of getting here)
        var linked = await _dbContext.LinkedAccounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Platform == LinkedPlatforms.Lichess && a.ExternalId == externalId, ct);
        if (linked != null)
        {
            if (!linked.User.IsActive)
                throw new LichessAuthException("This BlitzMate account is inactive.");
            Apply(linked, linked.User, lichessUser);
            linked.User.LastLogin = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("User {UserId} signed in with Lichess", linked.UserId);
            return new LichessSignInResult(linked.User, IsNewUser: false);
        }

        if (string.IsNullOrWhiteSpace(email))
            throw new LichessAuthException("Lichess didn't share your email address. Please allow it and try again.");
        email = email.Trim().ToLowerInvariant();

        // Don't attach Lichess to an existing account just because the emails
        // match: the owner has to sign in with their password and link it
        if (await _dbContext.Users.AnyAsync(u => u.Email == email, ct))
            throw new LichessSignInConflictException(
                $"There's already a BlitzMate account for {email}. Sign in with your password, " +
                "then connect Lichess from your profile.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = await PickUsernameAsync(lichessUser.Username, ct),
            // No password yet: an unguessable hash so password sign-in can't match
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastLogin = now
        };
        _dbContext.Users.Add(user);

        var account = new LinkedAccount
        {
            UserId = user.Id,
            Platform = LinkedPlatforms.Lichess,
            IsVerified = true,
            ConnectedAt = now
        };
        _dbContext.LinkedAccounts.Add(account);
        Apply(account, user, lichessUser);

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} signed up with Lichess account {LichessId}", user.Id, externalId);
        return new LichessSignInResult(user, IsNewUser: true);
    }

    // The Lichess username if it's free and fits BlitzMate's rules
    // (3-20 letters, digits or underscores), otherwise a numbered variant
    private async Task<string> PickUsernameAsync(string lichessUsername, CancellationToken ct)
    {
        var baseName = new string(lichessUsername.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        if (baseName.Length < 3)
            baseName = baseName.PadRight(3, '_');
        if (baseName.Length > 20)
            baseName = baseName[..20];

        if (!await _dbContext.Users.AnyAsync(u => u.Username == baseName, ct))
            return baseName;

        for (var n = 2; n < 1000; n++)
        {
            var suffix = $"_{n}";
            var candidate = baseName[..Math.Min(baseName.Length, 20 - suffix.Length)] + suffix;
            if (!await _dbContext.Users.AnyAsync(u => u.Username == candidate, ct))
                return candidate;
        }
        throw new InvalidOperationException($"Couldn't find a free username for '{lichessUsername}'.");
    }

    public async Task<LinkedAccountDto> RefreshAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await _dbContext.LinkedAccounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == LinkedPlatforms.Lichess, ct)
            ?? throw new KeyNotFoundException("No Lichess account linked");

        if (DateTime.UtcNow - account.LastSyncedAt < ManualRefreshCooldown)
            return LinkedAccountMapping.ToDto(account);

        var lichessUser = await _lichess.GetUserAsync(account.ExternalId, ct);
        if (lichessUser == null)
            throw new KeyNotFoundException("The linked Lichess account no longer exists");

        Apply(account, account.User, lichessUser);
        await _dbContext.SaveChangesAsync(ct);
        return LinkedAccountMapping.ToDto(account);
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.LinkedAccounts)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found");

        var account = user.LinkedAccounts.FirstOrDefault(a => a.Platform == LinkedPlatforms.Lichess);
        if (account != null)
            _dbContext.LinkedAccounts.Remove(account);

        user.LichessUsername = null;
        user.LichessUrl = null;
        user.LichessRating = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} unlinked Lichess", userId);
    }

    public async Task<int> SyncStaleAccountsAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var accounts = await _dbContext.LinkedAccounts
            .Include(a => a.User)
            .Where(a => a.Platform == LinkedPlatforms.Lichess && a.LastSyncedAt < cutoff)
            .OrderBy(a => a.LastSyncedAt)
            .Take(LichessClient.MaxUsersPerBatch)
            .ToListAsync(ct);

        if (accounts.Count == 0)
            return 0;

        var fetched = await _lichess.GetUsersAsync(accounts.Select(a => a.ExternalId).ToList(), ct);
        var byId = fetched.ToDictionary(u => u.Id.ToLowerInvariant());

        foreach (var account in accounts)
        {
            if (byId.TryGetValue(account.ExternalId, out var lichessUser))
                Apply(account, account.User, lichessUser);
            else
                // Closed accounts are omitted by Lichess; keep the last snapshot
                account.LastSyncedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return accounts.Count;
    }

    private void Apply(LinkedAccount account, User user, LichessUser source)
    {
        var now = DateTime.UtcNow;

        account.ExternalId = source.Id.ToLowerInvariant();
        account.Username = source.Username;
        account.ProfileUrl = source.Url ?? $"https://lichess.org/@/{source.Username}";
        account.Title = source.Title;
        account.Ratings = TrackedCategories
            .Where(c => source.Perfs != null && source.Perfs.TryGetValue(c, out var p) && p.Games > 0)
            .Select(c =>
            {
                var p = source.Perfs![c];
                return new PlatformRating
                {
                    Category = c,
                    Rating = p.Rating,
                    Games = p.Games,
                    Provisional = p.Prov,
                    Progress = p.Prog
                };
            })
            .ToList();
        // POST /api/users (batch sync) omits "count": keep the last known record
        if (source.Count != null)
        {
            account.GamesPlayed = source.Count.All;
            account.Wins = source.Count.Win;
            account.Losses = source.Count.Loss;
            account.Draws = source.Count.Draw;
        }
        account.PlatformJoinedAt = FromUnixMs(source.CreatedAt);
        account.PlatformLastSeenAt = FromUnixMs(source.SeenAt);
        account.LastSyncedAt = now;

        // Keep the denormalized User columns in sync: nearby/proposal/match
        // responses read the headline rating from there.
        var headline = PickHeadlineRating(account.Ratings);
        if (headline.HasValue && headline != user.LichessRating)
        {
            _dbContext.RatingSnapshots.Add(new RatingSnapshot
            {
                UserId = user.Id,
                Platform = LinkedPlatforms.Lichess,
                Rating = headline.Value,
                CapturedAt = now
            });
        }
        user.LichessUsername = account.Username;
        user.LichessUrl = account.ProfileUrl;
        user.LichessRating = headline;
        user.UpdatedAt = now;
    }

    // The established rating in the time control the player plays most,
    // falling back to provisional ratings when nothing is established yet.
    private static int? PickHeadlineRating(List<PlatformRating> ratings)
    {
        var candidates = ratings.Where(r => HeadlineCategories.Contains(r.Category)).ToList();
        var best = candidates.Where(r => !r.Provisional).MaxBy(r => r.Games)
                   ?? candidates.MaxBy(r => r.Games);
        return best?.Rating;
    }

    private static DateTime? FromUnixMs(long? ms) =>
        ms.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime : null;

    private static string CacheKey(string state) => $"lichess-oauth:{state}";

    private PendingAuth TakePending(string state)
    {
        if (!_cache.TryGetValue(CacheKey(state), out PendingAuth? pending) || pending == null)
            throw new LichessAuthException("This Lichess sign-in expired. Please try again.");
        _cache.Remove(CacheKey(state));
        return pending;
    }

    // Exchanges the code, reads who signed in (and their email when asked),
    // then revokes the token: BlitzMate never keeps Lichess access
    private async Task<(LichessUser User, string? Email)> ReadIdentityAsync(
        string code, string codeVerifier, bool withEmail, CancellationToken ct)
    {
        var accessToken = await _lichess.ExchangeCodeAsync(code, codeVerifier, ct);
        try
        {
            var user = await _lichess.GetAccountAsync(accessToken, ct);
            var email = withEmail ? await _lichess.GetEmailAsync(accessToken, ct) : null;
            return (user, email);
        }
        finally
        {
            try { await _lichess.RevokeTokenAsync(accessToken, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to revoke Lichess token"); }
        }
    }

    // UserId is null for sign-in, set for linking an account to a signed-in user
    private record PendingAuth(Guid? UserId, string CodeVerifier);
}

public record LichessSignInResult(User User, bool IsNewUser);

/// <summary>Signing in with Lichess would collide with an existing BlitzMate account.</summary>
public class LichessSignInConflictException : Exception
{
    public LichessSignInConflictException(string message) : base(message) { }
}
