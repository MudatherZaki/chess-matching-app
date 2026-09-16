using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Services;

namespace ChessApp.Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposalService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ProposalsController> _logger;

    public ProposalsController(
        IProposalService proposalService,
        IHubContext<NotificationHub> hubContext,
        ILogger<ProposalsController> logger)
    {
        _proposalService = proposalService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Create a new match proposal with a meeting location
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProposal([FromBody] CreateProposalRequest request)
    {
        try
        {
            var userId = GetUserId();
            var response = await _proposalService.CreateProposalAsync(userId, request);

            // Notify the receiver in real time, if connected
            var proposerUsername = User.FindFirst(ClaimTypes.Name)?.Value ?? "A player";
            await NotificationHub.SendProposalNotification(_hubContext, request.ReceiverId,
                new ProposalReceivedPayload
                {
                    ProposalId = response.Id,
                    ProposerId = userId,
                    ProposerUsername = proposerUsername,
                    Message = request.Message
                });

            return CreatedAtAction(nameof(CreateProposal), response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Create proposal failed: {Message}", ex.Message);
            return BadRequest(new ErrorResponse { Error = "ValidationError", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating proposal");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to create proposal" });
        }
    }

    /// <summary>
    /// Get proposals sent to the current user
    /// </summary>
    [HttpGet("incoming")]
    [ProducesResponseType(typeof(IncomingProposalsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncoming(
        [FromQuery] string status = "pending",
        [FromQuery] int limit = 20)
    {
        try
        {
            var userId = GetUserId();
            var response = await _proposalService.GetIncomingProposalsAsync(userId, status, limit);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching incoming proposals");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to fetch proposals" });
        }
    }

    /// <summary>
    /// Get proposals sent by the current user
    /// </summary>
    [HttpGet("outgoing")]
    [ProducesResponseType(typeof(OutgoingProposalsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutgoing(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            var userId = GetUserId();
            var response = await _proposalService.GetOutgoingProposalsAsync(userId, status, limit);
            return Ok(response);
        }
        catch (FormatException)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "ValidationError",
                Message = "Invalid status filter. Expected one of: pending, accepted, rejected, expired, cancelled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching outgoing proposals");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to fetch proposals" });
        }
    }

    /// <summary>
    /// Accept a pending proposal (only the receiver may do this)
    /// </summary>
    [HttpPost("{id}/accept")]
    [ProducesResponseType(typeof(AcceptProposalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptProposalRequest request)
    {
        try
        {
            var userId = GetUserId();
            var response = await _proposalService.AcceptProposalAsync(id, userId, request ?? new AcceptProposalRequest());

            // Look up the proposal's proposer so we can notify them - the response only has the proposal id,
            // so pull proposer id from the outgoing list is wasteful; instead notify by re-fetching via incoming
            // is unnecessary here since AcceptProposalResponse doesn't carry it. We notify using the receiver's
            // (current user's) identity as the responder; the hub looks up the proposer's connection separately.
            await NotifyProposerOfResponse(id, userId, response.Status);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse { Error = "Forbidden", Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "ValidationError", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting proposal {ProposalId}", id);
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to accept proposal" });
        }
    }

    /// <summary>
    /// Reject a pending proposal (only the receiver may do this)
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(typeof(RejectProposalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectProposalRequest request)
    {
        try
        {
            var userId = GetUserId();
            var response = await _proposalService.RejectProposalAsync(id, userId, request ?? new RejectProposalRequest());

            await NotifyProposerOfResponse(id, userId, response.Status);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse { Error = "Forbidden", Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "ValidationError", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting proposal {ProposalId}", id);
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to reject proposal" });
        }
    }

    /// <summary>
    /// Cancel a pending proposal (only the original proposer may do this)
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var userId = GetUserId();
            await _proposalService.CancelProposalAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = "NotFound", Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse { Error = "Forbidden", Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = "ValidationError", Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling proposal {ProposalId}", id);
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Failed to cancel proposal" });
        }
    }

    /// <summary>
    /// Notifies the proposer that their proposal was responded to. Looks the proposal
    /// back up via the incoming/outgoing lookup pattern is avoided - we fetch minimal
    /// info directly since AcceptProposalResponse/RejectProposalResponse don't carry the
    /// proposer id. This keeps the notification best-effort without failing the request
    /// if the lookup or the hub delivery fails.
    /// </summary>
    private async Task NotifyProposerOfResponse(Guid proposalId, Guid responderId, string status)
    {
        try
        {
            var proposerId = await _proposalService.GetProposerIdAsync(proposalId);
            if (proposerId == null) return;

            await NotificationHub.SendProposalRespondedNotification(_hubContext, proposerId.Value,
                new ProposalRespondedPayload
                {
                    ProposalId = proposalId,
                    Status = status,
                    ResponderId = responderId
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send real-time notification for proposal {ProposalId}", proposalId);
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
