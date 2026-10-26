using ChessApp.Backend.Data;
using ChessApp.Backend.Models;
using ChessApp.Backend.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChessApp.Backend.Tests;

public class LichessSignInTests
{
    private readonly ApplicationDbContext _db;
    private readonly FakeLichessClient _lichess = new();
    private readonly LichessIntegrationService _service;

    public LichessSignInTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _service = new LichessIntegrationService(
            _db, _lichess, new MemoryCache(new MemoryCacheOptions()), NullLogger<LichessIntegrationService>.Instance);
    }

    private Task<LichessSignInResult> SignInAsync()
    {
        var start = _service.StartSignIn();
        return _service.SignInAsync("code", start.State);
    }

    [Fact]
    public void StartSignIn_AsksForTheEmailScope()
    {
        var url = _service.StartSignIn().AuthorizeUrl;
        Assert.Equal("email:read", QueryHelpers.ParseQuery(new Uri(url).Query)["scope"]);
    }

    [Fact]
    public async Task FirstSignIn_CreatesAnAccountLinkedToLichess()
    {
        var result = await SignInAsync();

        Assert.True(result.IsNewUser);
        Assert.Equal("Magnus_Fan", result.User.Username);
        Assert.Equal("fan@example.com", result.User.Email);
        Assert.Equal("Magnus_Fan", result.User.LichessUsername);
        Assert.Equal(1890, result.User.LichessRating);

        var link = await _db.LinkedAccounts.SingleAsync();
        Assert.Equal(result.User.Id, link.UserId);
        Assert.Equal("magnus_fan", link.ExternalId);
        Assert.True(link.IsVerified);

        // No password was set, so password sign-in can't match anything
        Assert.False(BCrypt.Net.BCrypt.Verify("", result.User.PasswordHash));
        Assert.True(_lichess.Revoked);
    }

    [Fact]
    public async Task SignInAgain_ReturnsTheSameAccount()
    {
        var first = await SignInAsync();
        var second = await SignInAsync();

        Assert.False(second.IsNewUser);
        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Equal(1, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task SignIn_UsesAnAccountThatLinkedLichessFromItsProfile()
    {
        var existing = new User { Id = Guid.NewGuid(), Email = "other@example.com", Username = "zaki", PasswordHash = "x", IsActive = true };
        _db.Users.Add(existing);
        _db.LinkedAccounts.Add(new LinkedAccount
        {
            UserId = existing.Id, Platform = LinkedPlatforms.Lichess, ExternalId = "magnus_fan",
            Username = "Magnus_Fan", ProfileUrl = "https://lichess.org/@/Magnus_Fan", IsVerified = true
        });
        await _db.SaveChangesAsync();

        var result = await SignInAsync();

        Assert.False(result.IsNewUser);
        Assert.Equal(existing.Id, result.User.Id);
    }

    [Fact]
    public async Task EmailAlreadyRegistered_IsRefusedInsteadOfLinked()
    {
        _db.Users.Add(new User { Id = Guid.NewGuid(), Email = "fan@example.com", Username = "fan", PasswordHash = "x", IsActive = true });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<LichessSignInConflictException>(SignInAsync);
        Assert.Empty(_db.LinkedAccounts);
    }

    [Fact]
    public async Task TakenUsername_GetsANumberedVariant()
    {
        _db.Users.Add(new User { Id = Guid.NewGuid(), Email = "a@example.com", Username = "Magnus_Fan", PasswordHash = "x", IsActive = true });
        await _db.SaveChangesAsync();

        var result = await SignInAsync();

        Assert.Equal("Magnus_Fan_2", result.User.Username);
    }

    [Fact]
    public async Task LichessUsernamesOutsideBlitzMateRules_AreAdjusted()
    {
        _lichess.Account.Username = "a-b";

        var result = await SignInAsync();

        Assert.Equal("a_b", result.User.Username);
    }

    [Fact]
    public async Task NoEmail_FailsWithoutCreatingAnAccount()
    {
        _lichess.Email = null;

        await Assert.ThrowsAsync<LichessAuthException>(SignInAsync);
        Assert.Empty(_db.Users);
    }

    [Fact]
    public async Task LinkingState_CantBeUsedToSignIn()
    {
        var start = _service.StartAuthorization(Guid.NewGuid());

        await Assert.ThrowsAsync<LichessAuthException>(() => _service.SignInAsync("code", start.State));
    }

    [Fact]
    public async Task UnknownState_IsRejected()
    {
        await Assert.ThrowsAsync<LichessAuthException>(() => _service.SignInAsync("code", "made-up"));
    }

    private class FakeLichessClient : ILichessClient
    {
        public LichessUser Account { get; } = new()
        {
            Id = "magnus_fan",
            Username = "Magnus_Fan",
            Perfs = new() { ["blitz"] = new LichessPerf { Games = 120, Rating = 1890 } }
        };
        public string? Email { get; set; } = "Fan@Example.com";
        public bool Revoked { get; private set; }

        public string BuildAuthorizeUrl(string state, string codeChallenge, string? scope = null) =>
            QueryHelpers.AddQueryString("https://lichess.test/oauth", new Dictionary<string, string?>
            {
                ["state"] = state,
                ["scope"] = scope,
            });

        public Task<string> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default) =>
            Task.FromResult("lichess-token");

        public Task<LichessUser> GetAccountAsync(string accessToken, CancellationToken ct = default) =>
            Task.FromResult(Account);

        public Task<string?> GetEmailAsync(string accessToken, CancellationToken ct = default) =>
            Task.FromResult(Email);

        public Task RevokeTokenAsync(string accessToken, CancellationToken ct = default)
        {
            Revoked = true;
            return Task.CompletedTask;
        }

        public Task<LichessUser?> GetUserAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<LichessUser?>(Account);

        public Task<List<LichessUser>> GetUsersAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default) =>
            Task.FromResult(new List<LichessUser> { Account });
    }
}
