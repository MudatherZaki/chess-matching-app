namespace ChessApp.Backend.Models;

public class Block
{
    public Guid Id { get; set; }

    public Guid BlockerId { get; set; }
    public virtual User Blocker { get; set; } = null!;

    public Guid BlockedId { get; set; }
    public virtual User Blocked { get; set; } = null!;

    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
