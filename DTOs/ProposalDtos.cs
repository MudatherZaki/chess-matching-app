namespace ChessApp.Backend.DTOs;

public class CreateProposalRequest
{
    public Guid ReceiverId { get; set; }
    public string? Message { get; set; }

    // Meeting location where proposer wants to play
    public double MeetingLatitude { get; set; }
    public double MeetingLongitude { get; set; }
}

public class ProposalResponse
{
    public Guid Id { get; set; }
    public Guid ProposerId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
    public LocationDto? MeetingLocation { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProposalDetailResponse
{
    public Guid Id { get; set; }
    public UserPublicProfileResponse Proposer { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
    public LocationDto? MeetingLocation { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProposalWithDistanceDto
{
    public Guid Id { get; set; }
    public UserPublicProfileResponse Proposer { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
    public LocationDto? MeetingLocation { get; set; }
    public decimal DistanceFromYouKm { get; set; } // Distance from current user to meeting location
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IncomingProposalsResponse
{
    public List<ProposalWithDistanceDto> Proposals { get; set; } = new();
    public int Count => Proposals.Count;
}

public class OutgoingProposalsResponse
{
    public List<ProposalDetailResponse> Proposals { get; set; } = new();
    public int Count => Proposals.Count;
}

public class AcceptProposalRequest
{
    // Meeting location is already set in the proposal when it was created
    // Just need to confirm acceptance
}

public class AcceptProposalResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RespondedAt { get; set; }
    public Guid? MatchId { get; set; }
}

public class RejectProposalRequest
{
    public string? Reason { get; set; }
}

public class RejectProposalResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RespondedAt { get; set; }
}
