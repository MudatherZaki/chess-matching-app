namespace ChessApp.Backend.Models;

public class Review
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }
    public virtual Match Match { get; set; } = null!;

    public Guid ReviewerId { get; set; }
    public virtual User Reviewer { get; set; } = null!;

    public Guid RevieweeId { get; set; }
    public virtual User Reviewee { get; set; } = null!;

    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
