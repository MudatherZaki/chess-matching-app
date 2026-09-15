using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Services;

namespace ChessApp.Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var (user, accessToken, refreshToken) = await _authService.RegisterAsync(
                request.Email,
                request.Username,
                request.Password,
                request.FullName);

            var response = new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600
            };

            return CreatedAtAction(nameof(Register), response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(new ErrorResponse
            {
                Error = "RegistrationError",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var (accessToken, refreshToken) = await _authService.LoginAsync(
                request.Email,
                request.Password);

            var response = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed: {Message}", ex.Message);
            return Unauthorized(new ErrorResponse
            {
                Error = "AuthenticationError",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var accessToken = await _authService.RefreshAccessTokenAsync(request.RefreshToken);

            return Ok(new RefreshTokenResponse
            {
                AccessToken = accessToken,
                ExpiresIn = 3600
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidToken",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Logout (revoke refresh token)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Logout failed"
            });
        }
    }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserService userService, ILogger<ProfileController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetUserId();
            var profile = await _userService.GetProfileAsync(userId);
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to fetch profile"
            });
        }
    }

    /// <summary>
    /// Get public profile of another user
    /// </summary>
    [HttpGet("{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserPublicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        try
        {
            var profile = await _userService.GetPublicProfileAsync(userId);
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetUserId();
            var profile = await _userService.UpdateProfileAsync(userId, request);
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to update profile"
            });
        }
    }

    /// <summary>
    /// Upload profile photo
    /// </summary>
    [HttpPost("me/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPhoto([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ErrorResponse
                {
                    Error = "ValidationError",
                    Message = "No file provided"
                });

            if (file.Length > 5 * 1024 * 1024) // 5 MB
                return BadRequest(new ErrorResponse
                {
                    Error = "ValidationError",
                    Message = "File size exceeds 5 MB limit"
                });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new ErrorResponse
                {
                    Error = "ValidationError",
                    Message = "Only JPEG, PNG, and WebP images are allowed"
                });

            var userId = GetUserId();
            using (var stream = file.OpenReadStream())
            {
                var photoUrl = await _userService.UploadPhotoAsync(userId, stream, file.FileName);
                return Ok(new { photoUrl });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to upload photo"
            });
        }
    }

    // =====================================================
    // HELPER METHODS
    // =====================================================

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid user identity");

        return userId;
    }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        IUserService userService,
        IHubContext<NotificationHub> hubContext,
        ILogger<AvailabilityController> logger)
    {
        _userService = userService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Set user availability with location
    /// </summary>
    [HttpPost("set")]
    [ProducesResponseType(typeof(SetAvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAvailability([FromBody] SetAvailabilityRequest request)
    {
        try
        {
            var userId = GetUserId();
            var response = await _userService.SetAvailabilityAsync(userId, request);

            // Broadcast to all connected clients
            await NotificationHub.BroadcastAvailabilityChange(_hubContext,
                new UserAvailabilityChangedPayload
                {
                    UserId = userId,
                    IsAvailable = request.IsAvailable,
                    HasBoard = request.HasBoard,
                    Location = request.IsAvailable ? new LocationDto
                    {
                        Latitude = request.Latitude,
                        Longitude = request.Longitude
                    } : null
                });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting availability");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to set availability"
            });
        }
    }

    /// <summary>
    /// Get nearby available users
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(NearbyUsersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNearby(
        [FromQuery] int radiusKm = 10,
        [FromQuery] bool? hasBoard = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetUserId();
            var response = await _userService.FindNearbyUsersAsync(userId, radiusKm, hasBoard, limit);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "ValidationError",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching nearby users");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to fetch nearby users"
            });
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
