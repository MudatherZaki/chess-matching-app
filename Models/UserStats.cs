namespace ChessApp.Backend.Models;

public class UserStats
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int TotalMatchesPlayed { get; set; } = 0;
    public int TotalWins { get; set; } = 0;
    public int TotalLosses { get; set; } = 0;
    public int TotalDraws { get; set; } = 0;

    public int? AvgOpponentRating { get; set; }
    public int LongestStreak { get; set; } = 0;

    public DateTime LastStatsUpdate { get; set; } = DateTime.UtcNow;

    public int WinRate => TotalMatchesPlayed > 0
        ? (TotalWins * 100) / TotalMatchesPlayed
        : 0;
}
