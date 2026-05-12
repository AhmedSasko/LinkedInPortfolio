using System.Text.Json;
using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public class GoogleOAuthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GoogleOAuthService> logger) : IGoogleOAuthService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public string GetAuthorizationUrl(string state)
    {
        var clientId = configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId is not configured.");
        var redirectUri = configuration["Google:RedirectUri"]
            ?? throw new InvalidOperationException("Google:RedirectUri is not configured.");

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = "openid email profile",
            ["prompt"] = "select_account"
        };

        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{qs}";
    }

    public async Task<OAuthUserInfo> ExchangeCodeAsync(string code, string redirectUri)
    {
        var clientId = configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId is not configured.");
        var clientSecret = configuration["Google:ClientSecret"]
            ?? throw new InvalidOperationException("Google:ClientSecret is not configured.");

        var client = httpClientFactory.CreateClient();

        // Exchange authorization code for access token
        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            }));

        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenDoc = JsonDocument.Parse(tokenJson);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("No access_token in Google response.");

        logger.LogInformation("Google token exchanged successfully.");

        // Fetch user info using the access token
        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await client.SendAsync(userInfoRequest);
        userInfoResponse.EnsureSuccessStatusCode();

        var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
        logger.LogInformation("Google userinfo: {Json}", userInfoJson);

        var doc = JsonDocument.Parse(userInfoJson);
        var root = doc.RootElement;

        return new OAuthUserInfo(
            Sub: root.GetProperty("sub").GetString() ?? "",
            Name: root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            GivenName: root.TryGetProperty("given_name", out var gn) ? gn.GetString() : null,
            FamilyName: root.TryGetProperty("family_name", out var fn) ? fn.GetString() : null,
            Email: root.TryGetProperty("email", out var em) ? em.GetString() : null,
            EmailVerified: root.TryGetProperty("email_verified", out var ev) && ev.GetBoolean(),
            Picture: root.TryGetProperty("picture", out var pic) ? pic.GetString() : null
        );
    }
}
