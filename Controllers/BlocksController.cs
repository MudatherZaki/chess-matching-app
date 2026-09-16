using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Services;

namespace ChessApp.Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BlocksController : ControllerBase
{
    private readonly IBlockService _blockService;
    private readonly ILogger<BlocksController> _logger;

    public BlocksController(IBlockService blockService, ILogger<BlocksController> logger)
    {
        _blockService = blockService;
        _logger = logger;
    }

    /// <summary>
    /// Block a user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BlockDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        try
        {
            var userId = GetUserId();

            if (request.BlockedUserId == userId)
                return BadRequest(new ErrorResponse { Error = "ValidationError", Message = "You cannot block yourself" });

            var response = await _blockService.BlockUserAsync(userId, request);
            return CreatedAtAction(nameof(BlockUser), response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "ValidationError", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking user");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to block user" });
        }
    }

    /// <summary>
    /// Unblock a previously blocked user
    /// </summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockUser(Guid userId)
    {
        try
        {
            var currentUserId = GetUserId();
            await _blockService.UnblockUserAsync(currentUserId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking user");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to unblock user" });
        }
    }

    /// <summary>
    /// Get the current user's block list
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BlockListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlockList()
    {
        try
        {
            var userId = GetUserId();
            var response = await _blockService.GetBlockListAsync(userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching block list");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to fetch block list" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user identity");

        return userId;
    }
}
