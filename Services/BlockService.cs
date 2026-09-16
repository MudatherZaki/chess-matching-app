using Microsoft.EntityFrameworkCore;
using ChessApp.Backend.Data;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Models;

namespace ChessApp.Backend.Services;

public interface IBlockService
{
    Task<BlockDto> BlockUserAsync(Guid userId, BlockUserRequest request);

    Task<BlockListResponse> GetBlockListAsync(Guid userId);

    Task UnblockUserAsync(Guid userId, Guid blockedUserId);

    Task<bool> IsUserBlockedAsync(Guid userId, Guid potentialBlockerId);
}

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
