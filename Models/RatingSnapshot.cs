namespace ChessApp.Backend.Models;

public class RatingSnapshot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string Platform { get; set; } = null!; // "fide", "chesscom", "lichess"
    public int Rating { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
