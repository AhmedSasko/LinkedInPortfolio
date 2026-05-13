using System.Text.Json;
using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public class GoogleOAuthService(IConfiguration config, HttpClient http, ILogger<GoogleOAuthService> logger) : IOAuthService
{
    public string GetAuthorizationUrl(string state)
    {
        var clientId = config["Google:ClientId"] ?? throw new InvalidOperationException("Google:ClientId not configured");
        var redirectUri = config["Google:RedirectUri"] ?? throw new InvalidOperationException("Google:RedirectUri not configured");

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "offline"
        };

        return "https://accounts.google.com/o/oauth2/v2/auth?" +
            string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<OAuthUserInfo> ExchangeCodeAsync(string code, string redirectUri)
    {
        var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = config["Google:ClientId"]!,
                ["client_secret"] = config["Google:ClientSecret"]!
            }));

        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        var userReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        userReq.Headers.Authorization = new("Bearer", accessToken);
        var userResponse = await http.SendAsync(userReq);
        userResponse.EnsureSuccessStatusCode();

        var userJson = await userResponse.Content.ReadFromJsonAsync<JsonElement>();

        return new OAuthUserInfo(
            Sub: userJson.GetProperty("sub").GetString()!,
            Name: userJson.TryGetProperty("name", out var name) ? name.GetString() : null,
            Email: userJson.TryGetProperty("email", out var email) ? email.GetString() : null,
            Picture: userJson.TryGetProperty("picture", out var pic) ? pic.GetString() : null
        );
    }
}
