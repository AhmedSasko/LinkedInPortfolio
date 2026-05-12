using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkedInPortfolio.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService authService,
    ILinkedInOAuthService linkedInOAuth,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        try { _ = new System.Net.Mail.MailAddress(request.Email); }
        catch { return BadRequest(new { message = "Invalid email format." }); }

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var result = await authService.RegisterAsync(request.Email, request.Password);
        if (result is null)
            return BadRequest(new { message = "An account with this email already exists." });

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var result = await authService.LoginAsync(request.Email, request.Password);
        if (result is null)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(result);
    }

    [HttpGet("linkedin")]
    [AllowAnonymous]
    public IActionResult LinkedInLogin()
    {
        var state = Guid.NewGuid().ToString("N");
        // Store state in a short-lived cookie for CSRF protection
        Response.Cookies.Append("li_state", state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10)
        });
        var url = linkedInOAuth.GetAuthorizationUrl(state);
        return Redirect(url);
    }

    [HttpGet("linkedin/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> LinkedInCallback([FromQuery] string code, [FromQuery] string state)
    {
        // Validate state to prevent CSRF
        if (!Request.Cookies.TryGetValue("li_state", out var savedState) || savedState != state)
            return BadRequest("Invalid state parameter.");
        Response.Cookies.Delete("li_state");

        var redirectUri = configuration["LinkedIn:RedirectUri"]!;
        var frontendCallback = configuration["LinkedIn:FrontendCallbackUrl"]!;

        LinkedInUserInfo userInfo;
        try { userInfo = await linkedInOAuth.ExchangeCodeAsync(code, redirectUri); }
        catch (Exception ex)
        {
            return Redirect($"{frontendCallback}?error={Uri.EscapeDataString(ex.Message)}");
        }

        var authResult = await authService.HandleLinkedInLoginAsync(userInfo);

        // ProfileController's GET /api/profile/latest returns 404 if no profile, frontend handles it
        return Redirect($"{frontendCallback}?token={Uri.EscapeDataString(authResult.Token)}");
    }
}
