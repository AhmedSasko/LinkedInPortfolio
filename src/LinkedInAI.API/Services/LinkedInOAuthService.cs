using System.Text.Json;
using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public class LinkedInOAuthService(IConfiguration config, HttpClient http, ILogger<LinkedInOAuthService> logger) : IOAuthService
{
    public string GetAuthorizationUrl(string state)
    {
        var clientId = config["LinkedIn:ClientId"] ?? throw new InvalidOperationException("LinkedIn:ClientId not configured");
        var redirectUri = config["LinkedIn:RedirectUri"] ?? throw new InvalidOperationException("LinkedIn:RedirectUri not configured");

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = "openid profile email"
        };

        return "https://www.linkedin.com/oauth/v2/authorization?" +
            string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<OAuthUserInfo> ExchangeCodeAsync(string code, string redirectUri)
    {
        var tokenResponse = await http.PostAsync("https://www.linkedin.com/oauth/v2/accessToken",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = config["LinkedIn:ClientId"]!,
                ["client_secret"] = config["LinkedIn:ClientSecret"]!
            }));

        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        // LinkedIn OIDC userinfo endpoint
        var userReq = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
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
