using NetTopologySuite.Geometries;

namespace ChessApp.Backend.Models;

public class Match
{
    public Guid Id { get; set; }

    public Guid Player1Id { get; set; }
    public virtual User Player1 { get; set; } = null!;

    public Guid Player2Id { get; set; }
    public virtual User Player2 { get; set; } = null!;

    public Guid? ProposalId { get; set; }
    public virtual Proposal? Proposal { get; set; }

    public DateTime PlayedAt { get; set; }
    public Point? Location { get; set; } // Where they played

    public MatchOutcome Outcome { get; set; } = MatchOutcome.NotPlayed;
    public bool? PlayedWithBoard { get; set; }

    // Optional game data
    public string? TimeControl { get; set; } // e.g., "5+3", "10+0"
    public string? Pgn { get; set; } // Chess notation
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual List<Review> Reviews { get; set; } = new();
}
