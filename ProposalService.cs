using System;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ChessApp.Backend.Data;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Services;

public interface IProposalService
{
    Task<ProposalResponse> CreateProposalAsync(Guid proposerId, CreateProposalRequest request);
    
    Task<IncomingProposalsResponse> GetIncomingProposalsAsync(Guid userId, string status = "pending", int limit = 20);
    
    Task<OutgoingProposalsResponse> GetOutgoingProposalsAsync(Guid userId, string? status = null, int limit = 20);
    
    Task<AcceptProposalResponse> AcceptProposalAsync(Guid proposalId, AcceptProposalRequest request);
    
    Task<RejectProposalResponse> RejectProposalAsync(Guid proposalId, RejectProposalRequest request);
    
    Task CancelProposalAsync(Guid proposalId, Guid userId);
}

public interface IMatchService
{
    Task<MatchDto> RecordMatchAsync(Guid userId, CreateMatchRequest request);
    
    Task<MatchHistoryResponse> GetMatchHistoryAsync(Guid userId, int limit = 20, int offset = 0, string sortBy = "recent");
    
    Task UpdateMatchOutcomeAsync(Guid matchId, MatchOutcome outcome);
}

public interface IBlockService
{
    Task<BlockDto> BlockUserAsync(Guid userId, BlockUserRequest request);
    
    Task<BlockListResponse> GetBlockListAsync(Guid userId);
    
    Task UnblockUserAsync(Guid userId, Guid blockedUserId);
    
    Task<bool> IsUserBlockedAsync(Guid userId, Guid potentialBlockerId);
}

// =====================================================
// PROPOSAL SERVICE
// =====================================================

public class ProposalService : IProposalService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ProposalService> _logger;

    public ProposalService(ApplicationDbContext dbContext, ILogger<ProposalService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ProposalResponse> CreateProposalAsync(Guid proposerId, CreateProposalRequest request)
    {
        var proposer = await _dbContext.Users.FindAsync(proposerId);
        if (proposer == null)
            throw new KeyNotFoundException($"Proposer {proposerId} not found");

        var receiver = await _dbContext.Users.FindAsync(request.ReceiverId);
        if (receiver == null)
            throw new KeyNotFoundException($"Receiver {request.ReceiverId} not found");

        // Check if they're blocking each other
        var isBlocked = await _dbContext.Blocks.AnyAsync(b =>
            (b.BlockerId == proposerId && b.BlockedId == request.ReceiverId) ||
            (b.BlockerId == request.ReceiverId && b.BlockedId == proposerId));

        if (isBlocked)
            throw new InvalidOperationException("Cannot propose to a blocked user");

        // Check if receiver is available
        if (!receiver.IsAvailable || receiver.AvailabilityExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Receiver is not currently available");

        // Check for duplicate pending proposals
        var existingProposal = await _dbContext.Proposals.FirstOrDefaultAsync(p =>
            p.ProposerId == proposerId &&
            p.ReceiverId == request.ReceiverId &&
            p.Status == ProposalStatus.Pending &&
            p.ExpiresAt > DateTime.UtcNow);

        if (existingProposal != null)
            throw new InvalidOperationException("You already have a pending proposal to this user");

        // Create meeting location point
        var geometryFactory = new GeometryFactory(
            new NetTopologySuite.Geometries.PrecisionModel(), 4326);
        var meetingLocation = geometryFactory.CreatePoint(
            new NetTopologySuite.Geometries.Coordinate(request.MeetingLongitude, request.MeetingLatitude));

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            ProposerId = proposerId,
            ReceiverId = request.ReceiverId,
            Message = request.Message,
            MeetingLocation = meetingLocation,
            Status = ProposalStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Proposals.Add(proposal);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Proposal created: {ProposalId} from {ProposerId} to {ReceiverId} at location ({Lat},{Lon})",
            proposal.Id, proposerId, request.ReceiverId, request.MeetingLatitude, request.MeetingLongitude);

        return MapToProposalResponse(proposal);
    }

    public async Task<IncomingProposalsResponse> GetIncomingProposalsAsync(
        Guid userId, string status = "pending", int limit = 20)
    {
        var currentUser = await _dbContext.Users.FindAsync(userId);
        if (currentUser == null)
            throw new KeyNotFoundException($"User {userId} not found");

        var proposals = await _dbContext.Proposals
            .Include(p => p.Proposer)
            .ThenInclude(u => u.Stats)
            .Where(p =>
                p.ReceiverId == userId &&
                (status == "pending" ? p.Status == ProposalStatus.Pending :
                 status == "accepted" ? p.Status == ProposalStatus.Accepted :
                 status == "rejected" ? p.Status == ProposalStatus.Rejected : true))
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var response = new IncomingProposalsResponse();
        foreach (var proposal in proposals)
        {
            response.Proposals.Add(MapToProposalWithDistanceDto(proposal, currentUser.Location));
        }

        return response;
    }

    public async Task<OutgoingProposalsResponse> GetOutgoingProposalsAsync(
        Guid userId, string? status = null, int limit = 20)
    {
        var query = _dbContext.Proposals
            .Include(p => p.Receiver)
            .ThenInclude(u => u.Stats)
            .Where(p => p.ProposerId == userId);

        if (!string.IsNullOrEmpty(status))
        {
            var parsedStatus = Enum.Parse<ProposalStatus>(status, ignoreCase: true);
            query = query.Where(p => p.Status == parsedStatus);
        }

        var proposals = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var response = new OutgoingProposalsResponse();
        foreach (var proposal in proposals)
        {
            response.Proposals.Add(MapToOutgoingProposalDetailResponse(proposal));
        }

        return response;
    }

    public async Task<AcceptProposalResponse> AcceptProposalAsync(
        Guid proposalId, AcceptProposalRequest request)
    {
        var proposal = await _dbContext.Proposals
            .Include(p => p.Proposer)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
            throw new KeyNotFoundException($"Proposal {proposalId} not found");

        if (proposal.Status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot accept proposal with status {proposal.Status}");

        if (proposal.ExpiresAt <= DateTime.UtcNow)
        {
            proposal.Status = ProposalStatus.Expired;
            await _dbContext.SaveChangesAsync();
            throw new InvalidOperationException("Proposal has expired");
        }

        if (proposal.MeetingLocation == null)
            throw new InvalidOperationException("Proposal does not have a meeting location set");

        // Create match record using the proposal's meeting location
        var match = new Match
        {
            Id = Guid.NewGuid(),
            Player1Id = proposal.ProposerId,
            Player2Id = proposal.ReceiverId,
            ProposalId = proposalId,
            PlayedAt = DateTime.UtcNow,
            Location = proposal.MeetingLocation, // Use proposal's meeting location
            Outcome = MatchOutcome.NotPlayed,
            CreatedAt = DateTime.UtcNow
        };

        proposal.Status = ProposalStatus.Accepted;
        proposal.RespondedAt = DateTime.UtcNow;
        proposal.MatchId = match.Id;

        _dbContext.Matches.Add(match);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} accepted. Match {MatchId} created at meeting location",
            proposalId, match.Id);

        return new AcceptProposalResponse
        {
            Id = proposal.Id,
            Status = proposal.Status.ToString(),
            RespondedAt = proposal.RespondedAt.Value,
            MatchId = match.Id
        };
    }

    public async Task<RejectProposalResponse> RejectProposalAsync(
        Guid proposalId, RejectProposalRequest request)
    {
        var proposal = await _dbContext.Proposals.FindAsync(proposalId);

        if (proposal == null)
            throw new KeyNotFoundException($"Proposal {proposalId} not found");

        if (proposal.Status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot reject proposal with status {proposal.Status}");

        proposal.Status = ProposalStatus.Rejected;
        proposal.RespondedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} rejected", proposalId);

        return new RejectProposalResponse
        {
            Id = proposal.Id,
            Status = proposal.Status.ToString(),
            RespondedAt = proposal.RespondedAt.Value
        };
    }

    public async Task CancelProposalAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _dbContext.Proposals.FindAsync(proposalId);

        if (proposal == null)
            throw new KeyNotFoundException($"Proposal {proposalId} not found");

        if (proposal.ProposerId != userId)
            throw new UnauthorizedAccessException("Only the proposer can cancel");

        if (proposal.Status != ProposalStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel proposal with status {proposal.Status}");

        proposal.Status = ProposalStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} cancelled", proposalId);
    }

    private ProposalResponse MapToProposalResponse(Proposal proposal)
    {
        return new ProposalResponse
        {
            Id = proposal.Id,
            ProposerId = proposal.ProposerId,
            ReceiverId = proposal.ReceiverId,
            Status = proposal.Status.ToString(),
            Message = proposal.Message,
            MeetingLocation = proposal.MeetingLocation != null ? new LocationDto
            {
                Latitude = proposal.MeetingLocation.Coordinate.Y,
                Longitude = proposal.MeetingLocation.Coordinate.X
            } : null,
            MaxDistanceKm = proposal.MaxDistanceKm,
            ExpiresAt = proposal.ExpiresAt,
            CreatedAt = proposal.CreatedAt
        };
    }

    private ProposalDetailResponse MapToProposalDetailResponse(Proposal proposal)
    {
        return new ProposalDetailResponse
        {
            Id = proposal.Id,
            Proposer = MapToPublicProfile(proposal.Proposer),
            Status = proposal.Status.ToString(),
            Message = proposal.Message,
            ExpiresAt = proposal.ExpiresAt,
            CreatedAt = proposal.CreatedAt
        };
    }

    private ProposalDetailResponse MapToOutgoingProposalDetailResponse(Proposal proposal)
    {
        return new ProposalDetailResponse
        {
            Id = proposal.Id,
            Proposer = MapToPublicProfile(proposal.Receiver),
            Status = proposal.Status.ToString(),
            Message = proposal.Message,
            ExpiresAt = proposal.ExpiresAt,
            CreatedAt = proposal.CreatedAt
        };
    }

    private ProposalWithDistanceDto MapToProposalWithDistanceDto(Proposal proposal, NetTopologySuite.Geometries.Point? userLocation)
    {
        var distance = 0.0m;
        
        // Calculate distance from user to proposal's meeting location if both exist
        if (userLocation != null && proposal.MeetingLocation != null)
        {
            var distanceMeters = userLocation.Distance(proposal.MeetingLocation);
            distance = (decimal)Math.Round(distanceMeters / 1000.0, 2); // Convert to km
        }

        return new ProposalWithDistanceDto
        {
            Id = proposal.Id,
            Proposer = MapToPublicProfile(proposal.Proposer),
            Status = proposal.Status.ToString(),
            Message = proposal.Message,
            MeetingLocation = proposal.MeetingLocation != null ? new LocationDto
            {
                Latitude = proposal.MeetingLocation.Coordinate.Y,
                Longitude = proposal.MeetingLocation.Coordinate.X
            } : null,
            DistanceFromYouKm = distance,
            ExpiresAt = proposal.ExpiresAt,
            CreatedAt = proposal.CreatedAt
        };
    }

    private UserPublicProfileResponse MapToPublicProfile(User user)
    {
        return new UserPublicProfileResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            PhotoUrl = user.PhotoUrl,
            Bio = user.Bio,
            HasBoard = user.HasBoard,
            FideRating = user.FideRating,
            ChessComRating = user.ChessComRating,
            LichessRating = user.LichessRating,
            Stats = user.Stats != null ? MapToStatsDto(user.Stats) : null
        };
    }

    private UserStatsDto MapToStatsDto(UserStats stats)
    {
        return new UserStatsDto
        {
            TotalMatchesPlayed = stats.TotalMatchesPlayed,
            TotalWins = stats.TotalWins,
            TotalLosses = stats.TotalLosses,
            TotalDraws = stats.TotalDraws,
            WinRate = stats.WinRate,
            AvgOpponentRating = stats.AvgOpponentRating
        };
    }
}

// =====================================================
// MATCH SERVICE
// =====================================================

public class MatchService : IMatchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MatchService> _logger;

    public MatchService(ApplicationDbContext dbContext, ILogger<MatchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MatchDto> RecordMatchAsync(Guid userId, CreateMatchRequest request)
    {
        var player1 = await _dbContext.Users
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (player1 == null)
            throw new KeyNotFoundException($"User {userId} not found");

        var player2 = await _dbContext.Users
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == request.OpponentId);

        if (player2 == null)
            throw new KeyNotFoundException($"Opponent {request.OpponentId} not found");

        var outcome = Enum.Parse<MatchOutcome>(request.Outcome, ignoreCase: true);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Player1Id = player1.Id,
            Player2Id = player2.Id,
            PlayedAt = request.PlayedAt,
            Outcome = outcome,
            PlayedWithBoard = request.PlayedWithBoard,
            TimeControl = request.TimeControl,
            Pgn = request.Pgn,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        // Update player stats
        UpdatePlayerStats(player1, player2, outcome, isPlayer1: true);
        UpdatePlayerStats(player2, player1, outcome, isPlayer1: false);

        _dbContext.Matches.Add(match);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Match recorded: {MatchId} between {Player1} and {Player2}",
            match.Id, player1.Username, player2.Username);

        return MapToMatchDto(match, player2);
    }

    public async Task<MatchHistoryResponse> GetMatchHistoryAsync(
        Guid userId, int limit = 20, int offset = 0, string sortBy = "recent")
    {
        var query = _dbContext.Matches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .ThenInclude(u => u.Stats)
            .Where(m => m.Player1Id == userId || m.Player2Id == userId);

        var total = await query.CountAsync();

        var matches = sortBy == "oldest"
            ? await query.OrderBy(m => m.PlayedAt).Skip(offset).Take(limit).ToListAsync()
            : await query.OrderByDescending(m => m.PlayedAt).Skip(offset).Take(limit).ToListAsync();

        var response = new MatchHistoryResponse { Total = total };

        foreach (var match in matches)
        {
            var opponent = match.Player1Id == userId ? match.Player2 : match.Player1;
            response.Matches.Add(MapToMatchDto(match, opponent));
        }

        return response;
    }

    public async Task UpdateMatchOutcomeAsync(Guid matchId, MatchOutcome outcome)
    {
        var match = await _dbContext.Matches
            .Include(m => m.Player1)
            .ThenInclude(u => u.Stats)
            .Include(m => m.Player2)
            .ThenInclude(u => u.Stats)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null)
            throw new KeyNotFoundException($"Match {matchId} not found");

        match.Outcome = outcome;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Match {MatchId} outcome updated to {Outcome}", matchId, outcome);
    }

    private void UpdatePlayerStats(User player, User opponent, MatchOutcome outcome, bool isPlayer1)
    {
        if (player.Stats == null)
            return;

        player.Stats.TotalMatchesPlayed++;

        if (isPlayer1)
        {
            if (outcome == MatchOutcome.Player1Won)
                player.Stats.TotalWins++;
            else if (outcome == MatchOutcome.Player2Won)
                player.Stats.TotalLosses++;
            else if (outcome == MatchOutcome.Draw)
                player.Stats.TotalDraws++;
        }
        else
        {
            if (outcome == MatchOutcome.Player2Won)
                player.Stats.TotalWins++;
            else if (outcome == MatchOutcome.Player1Won)
                player.Stats.TotalLosses++;
            else if (outcome == MatchOutcome.Draw)
                player.Stats.TotalDraws++;
        }

        player.Stats.LastStatsUpdate = DateTime.UtcNow;
    }

    private MatchDto MapToMatchDto(Match match, User opponent)
    {
        return new MatchDto
        {
            Id = match.Id,
            Opponent = new UserPublicProfileResponse
            {
                Id = opponent.Id,
                Username = opponent.Username,
                FullName = opponent.FullName,
                PhotoUrl = opponent.PhotoUrl,
                FideRating = opponent.FideRating,
                ChessComRating = opponent.ChessComRating,
                LichessRating = opponent.LichessRating,
                Stats = opponent.Stats != null ? MapToStatsDto(opponent.Stats) : null
            },
            PlayedAt = match.PlayedAt,
            Outcome = match.Outcome.ToString(),
            PlayedWithBoard = match.PlayedWithBoard,
            TimeControl = match.TimeControl,
            Notes = match.Notes,
            CreatedAt = match.CreatedAt
        };
    }

    private UserStatsDto MapToStatsDto(UserStats stats)
    {
        return new UserStatsDto
        {
            TotalMatchesPlayed = stats.TotalMatchesPlayed,
            TotalWins = stats.TotalWins,
            TotalLosses = stats.TotalLosses,
            TotalDraws = stats.TotalDraws,
            WinRate = stats.WinRate,
            AvgOpponentRating = stats.AvgOpponentRating
        };
    }
}

// =====================================================
// BLOCK SERVICE
// =====================================================

public class BlockService : IBlockService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BlockService> _logger;

    public BlockService(ApplicationDbContext dbContext, ILogger<BlockService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BlockDto> BlockUserAsync(Guid userId, BlockUserRequest request)
    {
        var blockedUser = await _dbContext.Users.FindAsync(request.BlockedUserId);
        if (blockedUser == null)
            throw new KeyNotFoundException($"User {request.BlockedUserId} not found");

        var existingBlock = await _dbContext.Blocks
            .FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == request.BlockedUserId);

        if (existingBlock != null)
            throw new InvalidOperationException("User already blocked");

        var block = new Block
        {
            Id = Guid.NewGuid(),
            BlockerId = userId,
            BlockedId = request.BlockedUserId,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Blocks.Add(block);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} blocked {BlockedUserId}", userId, request.BlockedUserId);

        return new BlockDto
        {
            Id = block.Id,
            BlockedUserId = block.BlockedId,
            BlockedUsername = blockedUser.Username,
            BlockedAt = block.CreatedAt,
            Reason = block.Reason
        };
    }

    public async Task<BlockListResponse> GetBlockListAsync(Guid userId)
    {
        var blocks = await _dbContext.Blocks
            .Include(b => b.Blocked)
            .Where(b => b.BlockerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var response = new BlockListResponse();
        foreach (var block in blocks)
        {
            response.BlockedUsers.Add(new BlockDto
            {
                Id = block.Id,
                BlockedUserId = block.BlockedId,
                BlockedUsername = block.Blocked.Username,
                BlockedAt = block.CreatedAt,
                Reason = block.Reason
            });
        }

        return response;
    }

    public async Task UnblockUserAsync(Guid userId, Guid blockedUserId)
    {
        var block = await _dbContext.Blocks
            .FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == blockedUserId);

        if (block == null)
            throw new KeyNotFoundException("Block not found");

        _dbContext.Blocks.Remove(block);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} unblocked {BlockedUserId}", userId, blockedUserId);
    }

    public async Task<bool> IsUserBlockedAsync(Guid userId, Guid potentialBlockerId)
    {
        return await _dbContext.Blocks.AnyAsync(b =>
            (b.BlockerId == userId && b.BlockedId == potentialBlockerId) ||
            (b.BlockerId == potentialBlockerId && b.BlockedId == userId));
    }
}
