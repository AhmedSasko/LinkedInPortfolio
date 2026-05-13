using System.Security.Claims;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInAI.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService authService,
    IConfiguration config,
    ILogger<AuthController> logger,
    IProfileService profileService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await authService.RevokeTokenAsync(request.RefreshToken, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var state = Guid.NewGuid().ToString("N");
        Response.Cookies.Append("oauth_state", state, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10)
        });

        var googleService = HttpContext.RequestServices
            .GetRequiredKeyedService<IOAuthService>("google");
        var url = googleService.GetAuthorizationUrl(state);
        return Redirect(url);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        var savedState = Request.Cookies["oauth_state"];
        if (string.IsNullOrEmpty(savedState) || savedState != state)
            return Redirect(GetFrontendErrorUrl("Invalid OAuth state"));

        try
        {
            var googleService = HttpContext.RequestServices
                .GetRequiredKeyedService<IOAuthService>("google");
            var redirectUri = config["Google:RedirectUri"]!;
            var userInfo = await googleService.ExchangeCodeAsync(code, redirectUri);
            var authResponse = await authService.HandleOAuthLoginAsync(userInfo);

            Response.Cookies.Delete("oauth_state");
            return Redirect($"{config["Google:FrontendCallbackUrl"]}?token={Uri.EscapeDataString(authResponse.AccessToken)}&refreshToken={Uri.EscapeDataString(authResponse.RefreshToken)}&userId={authResponse.UserId}&email={Uri.EscapeDataString(authResponse.Email)}&isAdmin={authResponse.IsAdmin}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Google OAuth callback failed");
            return Redirect(GetFrontendErrorUrl("Authentication failed"));
        }
    }

    [HttpGet("linkedin")]
    public IActionResult LinkedInLogin()
    {
        var state = Guid.NewGuid().ToString("N");
        Response.Cookies.Append("oauth_state", state, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10)
        });

        var linkedInService = HttpContext.RequestServices
            .GetRequiredKeyedService<IOAuthService>("linkedin");
        var url = linkedInService.GetAuthorizationUrl(state);
        return Redirect(url);
    }

    [HttpGet("linkedin/callback")]
    public async Task<IActionResult> LinkedInCallback([FromQuery] string code, [FromQuery] string state)
    {
        var savedState = Request.Cookies["oauth_state"];
        if (string.IsNullOrEmpty(savedState) || savedState != state)
            return Redirect(GetFrontendErrorUrl("Invalid OAuth state"));

        try
        {
            var linkedInService = HttpContext.RequestServices
                .GetRequiredKeyedService<IOAuthService>("linkedin");
            var redirectUri = config["LinkedIn:RedirectUri"]!;
            var userInfo = await linkedInService.ExchangeCodeAsync(code, redirectUri);
            var authResponse = await authService.HandleOAuthLoginAsync(userInfo, userInfo.Sub);

            Response.Cookies.Delete("oauth_state");
            return Redirect($"{config["Frontend:CallbackUrl"]}?token={Uri.EscapeDataString(authResponse.AccessToken)}&refreshToken={Uri.EscapeDataString(authResponse.RefreshToken)}&userId={authResponse.UserId}&email={Uri.EscapeDataString(authResponse.Email)}&isAdmin={authResponse.IsAdmin}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LinkedIn OAuth callback failed");
            return Redirect(GetFrontendErrorUrl("Authentication failed"));
        }
    }

    [Authorize]
    [HttpGet("linkedin/sync-url")]
    public IActionResult GetLinkedInSyncUrl()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var nonce = Guid.NewGuid().ToString("N");
        var state = $"{nonce}:{userId}";

        Response.Cookies.Append("linkedin_sync_state", nonce, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10)
        });

        var clientId = config["LinkedIn:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return BadRequest(new { message = "LinkedIn OAuth is not configured on this server." });
        var syncRedirectUri = config["LinkedIn:SyncRedirectUri"] ?? throw new InvalidOperationException("LinkedIn:SyncRedirectUri not configured");

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = syncRedirectUri,
            ["state"] = state,
            ["scope"] = "openid profile email"
        };

        var url = "https://www.linkedin.com/oauth/v2/authorization?" +
            string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return Ok(new { url });
    }

    [HttpGet("linkedin/sync-callback")]
    public async Task<IActionResult> LinkedInSyncCallback([FromQuery] string code, [FromQuery] string state)
    {
        var savedNonce = Request.Cookies["linkedin_sync_state"];
        if (string.IsNullOrEmpty(savedNonce))
            return Redirect(GetFrontendErrorUrl("Missing LinkedIn sync state"));

        var parts = state.Split(':');
        if (parts.Length != 2 || parts[0] != savedNonce)
            return Redirect(GetFrontendErrorUrl("Invalid LinkedIn sync state"));

        if (!int.TryParse(parts[1], out var userId))
            return Redirect(GetFrontendErrorUrl("Invalid user in sync state"));

        try
        {
            var syncRedirectUri = config["LinkedIn:SyncRedirectUri"]!;
            var linkedInService = HttpContext.RequestServices
                .GetRequiredKeyedService<IOAuthService>("linkedin");
            var userInfo = await linkedInService.ExchangeCodeAsync(code, syncRedirectUri);

            await profileService.SyncFromLinkedInAsync(userId, userInfo.Name, userInfo.Picture);

            Response.Cookies.Delete("linkedin_sync_state");
            return Redirect($"{config["Frontend:BaseUrl"]}/edit?synced=true");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LinkedIn sync callback failed for user {UserId}", userId);
            return Redirect(GetFrontendErrorUrl("LinkedIn sync failed"));
        }
    }

    private string GetFrontendErrorUrl(string error) =>
        $"{config["Frontend:CallbackUrl"]}?error={Uri.EscapeDataString(error)}";
}
