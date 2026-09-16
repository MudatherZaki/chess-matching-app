using Microsoft.EntityFrameworkCore;
using ChessApp.Backend.Data;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Services;

public interface IMatchService
{
    Task<MatchDto> RecordMatchAsync(Guid userId, CreateMatchRequest request);

    Task<MatchHistoryResponse> GetMatchHistoryAsync(Guid userId, int limit = 20, int offset = 0, string sortBy = "recent");

    Task UpdateMatchOutcomeAsync(Guid matchId, MatchOutcome outcome);
}

public class MatchService : IMatchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MatchService> _logger;

    public MatchService(ApplicationDbContext dbContext, ILogger<MatchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MatchDto> RecordMatchAsync(Guid userId, CreateMatchRequest request)
    {
        var player1 = await _dbContext.Users
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (player1 == null)
            throw new KeyNotFoundException($"User {userId} not found");

        var player2 = await _dbContext.Users
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == request.OpponentId);

        if (player2 == null)
            throw new KeyNotFoundException($"Opponent {request.OpponentId} not found");

        var outcome = ParseOutcome(request.Outcome);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Player1Id = player1.Id,
            Player2Id = player2.Id,
            PlayedAt = request.PlayedAt,
            Outcome = outcome,
            PlayedWithBoard = request.PlayedWithBoard,
            TimeControl = request.TimeControl,
            Pgn = request.Pgn,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        // Update player stats
        UpdatePlayerStats(player1, player2, outcome, isPlayer1: true);
        UpdatePlayerStats(player2, player1, outcome, isPlayer1: false);

        _dbContext.Matches.Add(match);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Match recorded: {MatchId} between {Player1} and {Player2}",
            match.Id, player1.Username, player2.Username);

        return MapToMatchDto(match, player2);
    }

    public async Task<MatchHistoryResponse> GetMatchHistoryAsync(
        Guid userId, int limit = 20, int offset = 0, string sortBy = "recent")
    {
        var query = _dbContext.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .ThenInclude(u => u.Stats)
            .Where(m => m.Player1Id == userId || m.Player2Id == userId);

        var total = await query.CountAsync();

        var matches = sortBy == "oldest"
            ? await query.OrderBy(m => m.PlayedAt).Skip(offset).Take(limit).ToListAsync()
            : await query.OrderByDescending(m => m.PlayedAt).Skip(offset).Take(limit).ToListAsync();

        var response = new MatchHistoryResponse { Total = total };

        foreach (var match in matches)
        {
            var opponent = match.Player1Id == userId ? match.Player2 : match.Player1;
            response.Matches.Add(MapToMatchDto(match, opponent));
        }

        return response;
    }

    public async Task UpdateMatchOutcomeAsync(Guid matchId, MatchOutcome outcome)
    {
        var match = await _dbContext.Matches
            .Include(m => m.Player1)
            .ThenInclude(u => u.Stats)
            .Include(m => m.Player2)
            .ThenInclude(u => u.Stats)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null)
            throw new KeyNotFoundException($"Match {matchId} not found");

        match.Outcome = outcome;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Match {MatchId} outcome updated to {Outcome}", matchId, outcome);
    }

    private void UpdatePlayerStats(User player, User opponent, MatchOutcome outcome, bool isPlayer1)
    {
        if (player.Stats == null)
            return;

        player.Stats.TotalMatchesPlayed++;

        if (isPlayer1)
        {
            if (outcome == MatchOutcome.Player1Won)
                player.Stats.TotalWins++;
            else if (outcome == MatchOutcome.Player2Won)
                player.Stats.TotalLosses++;
            else if (outcome == MatchOutcome.Draw)
                player.Stats.TotalDraws++;
        }
        else
        {
            if (outcome == MatchOutcome.Player2Won)
                player.Stats.TotalWins++;
            else if (outcome == MatchOutcome.Player1Won)
                player.Stats.TotalLosses++;
            else if (outcome == MatchOutcome.Draw)
                player.Stats.TotalDraws++;
        }

        player.Stats.LastStatsUpdate = DateTime.UtcNow;
    }

    private static MatchOutcome ParseOutcome(string outcome)
    {
        // Mobile sends snake_case (e.g. "player1_won"); the enum is PascalCase.
        var normalized = outcome.Replace("_", "");
        if (Enum.TryParse<MatchOutcome>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Invalid outcome '{outcome}'. Expected one of: not_played, player1_won, player2_won, draw");
    }

    private static string FormatOutcome(MatchOutcome outcome)
    {
        return outcome switch
        {
            MatchOutcome.NotPlayed => "not_played",
            MatchOutcome.Player1Won => "player1_won",
            MatchOutcome.Player2Won => "player2_won",
            MatchOutcome.Draw => "draw",
            _ => outcome.ToString().ToLowerInvariant()
        };
    }

    private MatchDto MapToMatchDto(Match match, User opponent)
    {
        return new MatchDto
        {
            Id = match.Id,
            Opponent = new UserPublicProfileResponse
            {
                Id = opponent.Id,
                Username = opponent.Username,
                FullName = opponent.FullName,
                PhotoUrl = opponent.PhotoUrl,
                FideRating = opponent.FideRating,
                ChessComRating = opponent.ChessComRating,
                LichessRating = opponent.LichessRating,
                Stats = opponent.Stats != null ? MapToStatsDto(opponent.Stats) : null
            },
            PlayedAt = match.PlayedAt,
            Outcome = FormatOutcome(match.Outcome),
            PlayedWithBoard = match.PlayedWithBoard,
            TimeControl = match.TimeControl,
            Notes = match.Notes,
            CreatedAt = match.CreatedAt
        };
    }

    private UserStatsDto MapToStatsDto(UserStats stats)
    {
        return new UserStatsDto
        {
            TotalMatchesPlayed = stats.TotalMatchesPlayed,
            TotalWins = stats.TotalWins,
            TotalLosses = stats.TotalLosses,
            TotalDraws = stats.TotalDraws,
            WinRate = stats.WinRate,
            AvgOpponentRating = stats.AvgOpponentRating
        };
    }
}
