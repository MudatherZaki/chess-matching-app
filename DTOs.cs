namespace ChessApp.Backend.DTOs;

// =====================================================
// AUTH DTOS
// =====================================================

public class RegisterRequest
{
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class AuthResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public int ExpiresIn { get; set; } = 3600;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = null!;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = null!;
    public int ExpiresIn { get; set; } = 3600;
}

// =====================================================
// PROFILE DTOS
// =====================================================

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? Bio { get; set; }
    public bool? HasBoard { get; set; }
    public string? FideId { get; set; }
    public int? FideRating { get; set; }
    public string? ChessComUsername { get; set; }
    public string? LichessUsername { get; set; }
}

public class ChessRatingDto
{
    public string? Username { get; set; }
    public string? Url { get; set; }
    public int? Rating { get; set; }
}

public class UserStatsDto
{
    public int TotalMatchesPlayed { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int WinRate { get; set; }
    public int? AvgOpponentRating { get; set; }
}

public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? FullName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }

    public bool IsAvailable { get; set; }
    public DateTime? AvailabilityExpiresAt { get; set; }
    public bool HasBoard { get; set; }

    public string? FideId { get; set; }
    public int? FideRating { get; set; }

    public ChessRatingDto? ChessCom { get; set; }
    public ChessRatingDto? Lichess { get; set; }

    public UserStatsDto? Stats { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class UserPublicProfileResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string? FullName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }

    public bool HasBoard { get; set; }
    public int? FideRating { get; set; }
    public int? ChessComRating { get; set; }
    public int? LichessRating { get; set; }

    public UserStatsDto? Stats { get; set; }
}

// =====================================================
// AVAILABILITY & DISCOVERY DTOS
// =====================================================

public class SetAvailabilityRequest
{
    public bool IsAvailable { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool HasBoard { get; set; }
    public int ExpiresInHours { get; set; } = 4;
}

public class SetAvailabilityResponse
{
    public bool IsAvailable { get; set; }
    public DateTime ExpiresAt { get; set; }
    public LocationDto Location { get; set; } = null!;
}

public class LocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
}

public class NearbyUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string? FullName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }

    public int? FideRating { get; set; }
    public int? ChessComRating { get; set; }
    public int? LichessRating { get; set; }

    public bool HasBoard { get; set; }
    public decimal DistanceKm { get; set; }

    public UserStatsDto? Stats { get; set; }
}

public class NearbyUsersResponse
{
    public List<NearbyUserDto> Users { get; set; } = new();
    public int Count => Users.Count;
}

// =====================================================
// PROPOSAL DTOS
// =====================================================

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

// =====================================================
// MATCH DTOS
// =====================================================

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

// =====================================================
// BLOCK DTOS
// =====================================================

public class BlockUserRequest
{
    public Guid BlockedUserId { get; set; }
    public string? Reason { get; set; }
}

public class BlockDto
{
    public Guid Id { get; set; }
    public Guid BlockedUserId { get; set; }
    public string? BlockedUsername { get; set; }
    public DateTime BlockedAt { get; set; }
    public string? Reason { get; set; }
}

public class BlockListResponse
{
    public List<BlockDto> BlockedUsers { get; set; } = new();
    public int Count => BlockedUsers.Count;
}

// =====================================================
// ERROR DTOS
// =====================================================

public class ErrorResponse
{
    public string Error { get; set; } = null!;
    public string Message { get; set; } = null!;
    public List<FieldError>? Details { get; set; }
}

public class FieldError
{
    public string Field { get; set; } = null!;
    public string Message { get; set; } = null!;
}

public class ValidationErrorResponse
{
    public string Error { get; set; } = "ValidationError";
    public string Message { get; set; } = "Validation failed";
    public List<FieldError> Details { get; set; } = new();
}

// =====================================================
// WEBSOCKET / REAL-TIME DTOS
// =====================================================

public class WebSocketMessageDto<T>
{
    public string Type { get; set; } = null!;
    public T Payload { get; set; } = default!;
}

public class ProposalReceivedPayload
{
    public Guid ProposalId { get; set; }
    public Guid ProposerId { get; set; }
    public string ProposerUsername { get; set; } = null!;
    public string? ProposerPhotoUrl { get; set; }
    public string? Message { get; set; }
}

public class ProposalRespondedPayload
{
    public Guid ProposalId { get; set; }
    public string Status { get; set; } = null!;
    public Guid ResponderId { get; set; }
}

public class UserAvailabilityChangedPayload
{
    public Guid UserId { get; set; }
    public bool IsAvailable { get; set; }
    public bool HasBoard { get; set; }
    public LocationDto? Location { get; set; }
}

public class UserCameOnlinePayload
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
}
