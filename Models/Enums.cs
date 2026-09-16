namespace ChessApp.Backend.Models;

public enum ProposalStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired,
    Cancelled
}

public enum MatchOutcome
{
    NotPlayed,
    Player1Won,
    Player2Won,
    Draw
}
