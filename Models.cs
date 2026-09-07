using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace ChessApp.Backend.Models;

// =====================================================
// ENUMS
// =====================================================

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

// =====================================================
// USERS
// =====================================================

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? FullName { get; set; }

    // Profile
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }

    // Location (PostGIS Point)
    public Point? Location { get; set; }
    public DateTime? LastLocationUpdate { get; set; }

    // Availability
    public bool IsAvailable { get; set; } = false;
    public DateTime? AvailabilityExpiresAt { get; set; }
    public bool HasBoard { get; set; } = false;

    // Chess credentials - FIDE
    public string? FideId { get; set; }
    public int? FideRating { get; set; }
    public DateTime? FideRatingUpdatedAt { get; set; }

    // Chess.com
    public string? ChessComUsername { get; set; }
    public string? ChessComUrl { get; set; }
    public int? ChessComRating { get; set; }

    // Lichess
    public string? LichessUsername { get; set; }
    public string? LichessUrl { get; set; }
    public int? LichessRating { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;

    public List<string> DeviceTokens { get; set; } = new();

    // Navigation
    public virtual List<Proposal> ProposalsSent { get; set; } = new();
    public virtual List<Proposal> ProposalsReceived { get; set; } = new();
    public virtual List<Match> MatchesAsPlayer1 { get; set; } = new();
    public virtual List<Match> MatchesAsPlayer2 { get; set; } = new();
    public virtual UserStats? Stats { get; set; }
    public virtual List<Block> BlockedBy { get; set; } = new();
    public virtual List<Block> Blocking { get; set; } = new();
    public virtual List<RefreshToken> RefreshTokens { get; set; } = new();
    public virtual List<RatingSnapshot> RatingSnapshots { get; set; } = new();
}

// =====================================================
// PROPOSALS / MATCH REQUESTS
// =====================================================

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

// =====================================================
// MATCHES / COMPLETED GAMES
// =====================================================

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
}

// =====================================================
// USER STATISTICS
// =====================================================

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

// =====================================================
// BLOCKS / REPORTS
// =====================================================

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

// =====================================================
// RATING SNAPSHOTS (Historical tracking)
// =====================================================

public class RatingSnapshot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string Platform { get; set; } = null!; // "fide", "chesscom", "lichess"
    public int Rating { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

// =====================================================
// AUTHENTICATION
// =====================================================

public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;
}
