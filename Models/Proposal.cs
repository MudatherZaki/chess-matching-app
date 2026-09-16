using NetTopologySuite.Geometries;

namespace ChessApp.Backend.Models;

public class Proposal
{
    public Guid Id { get; set; }

    public Guid ProposerId { get; set; }
    public virtual User Proposer { get; set; } = null!;

    public Guid ReceiverId { get; set; }
    public virtual User Receiver { get; set; } = null!;

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public DateTime? RespondedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    public string? Message { get; set; }

    // Meeting location where proposer wants to play
    public Point? MeetingLocation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional link to accepted match
    public Guid? MatchId { get; set; }
    public virtual Match? Match { get; set; }
}
