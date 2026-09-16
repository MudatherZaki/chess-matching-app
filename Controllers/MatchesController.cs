using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Services;

namespace ChessApp.Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly ILogger<MatchesController> _logger;

    public MatchesController(IMatchService matchService, ILogger<MatchesController> logger)
    {
        _matchService = matchService;
        _logger = logger;
    }

    /// <summary>
    /// Record a completed match and update both players' stats
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MatchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordMatch([FromBody] CreateMatchRequest request)
    {
        try
        {
            var userId = GetUserId();
            var response = await _matchService.RecordMatchAsync(userId, request);
            return CreatedAtAction(nameof(RecordMatch), response);
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
            _logger.LogError(ex, "Error recording match");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to record match" });
        }
    }

    /// <summary>
    /// Get the current user's match history
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(MatchHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        [FromQuery] string sortBy = "recent")
    {
        try
        {
            var userId = GetUserId();
            var response = await _matchService.GetMatchHistoryAsync(userId, limit, offset, sortBy);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching match history");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to fetch match history" });
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
