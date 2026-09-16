namespace ChessApp.Backend.DTOs;

public class CreateMatchRequest
{
    public Guid OpponentId { get; set; }
    public DateTime PlayedAt { get; set; }
    public string Outcome { get; set; } = null!; // "player1_won", "player2_won", "draw"
    public bool? PlayedWithBoard { get; set; }
    public string? TimeControl { get; set; }
    public string? Pgn { get; set; }
    public string? Notes { get; set; }
}

public class MatchDto
{
    public Guid Id { get; set; }
    public UserPublicProfileResponse Opponent { get; set; } = null!;
    public DateTime PlayedAt { get; set; }
    public string Outcome { get; set; } = null!;
    public bool? PlayedWithBoard { get; set; }
    public string? TimeControl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MatchHistoryResponse
{
    public List<MatchDto> Matches { get; set; } = new();
    public int Count => Matches.Count;
    public int Total { get; set; }
}
