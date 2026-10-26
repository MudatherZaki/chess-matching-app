using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChessApp.Backend.DTOs;
using ChessApp.Backend.Services;

namespace ChessApp.Backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILichessIntegrationService _lichess;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILichessIntegrationService lichess,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _lichess = lichess;
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
            var (user, accessToken, refreshToken) = await _authService.LoginAsync(
                request.Email,
                request.Password);

            var response = new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
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

    /// <summary>
    /// Start signing in (or up) with Lichess. Open the returned URL in a browser;
    /// Lichess redirects back to the app's deep link with ?code=...&amp;state=...
    /// </summary>
    [HttpPost("lichess/authorize")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LichessAuthorizeResponse), StatusCodes.Status200OK)]
    public IActionResult AuthorizeLichess()
    {
        return Ok(_lichess.StartSignIn());
    }

    /// <summary>
    /// Finish signing in with Lichess. Signs in the player who has that Lichess
    /// account linked, or creates a new account from it (IsNewUser = true).
    /// </summary>
    [HttpPost("lichess/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> LichessCallback([FromBody] LichessCallbackRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _lichess.SignInAsync(request.Code, request.State, ct);
            var (accessToken, refreshToken) = await _authService.IssueTokensAsync(result.User);

            return Ok(new AuthResponse
            {
                UserId = result.User.Id,
                Email = result.User.Email,
                Username = result.User.Username,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600,
                IsNewUser = result.IsNewUser
            });
        }
        catch (LichessAuthException ex)
        {
            _logger.LogWarning("Lichess sign-in failed: {Message}", ex.Message);
            return BadRequest(new ErrorResponse { Error = "LichessAuthFailed", Message = ex.Message });
        }
        catch (LichessSignInConflictException ex)
        {
            return Conflict(new ErrorResponse { Error = "AccountExists", Message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lichess unreachable during sign-in");
            return StatusCode(502, new ErrorResponse { Error = "LichessUnavailable", Message = "Could not reach Lichess. Please try again." });
        }
    }
}
