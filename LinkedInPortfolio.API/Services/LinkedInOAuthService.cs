using System.Text.Json;
using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public class LinkedInOAuthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<LinkedInOAuthService> logger) : ILinkedInOAuthService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public string GetAuthorizationUrl(string state)
    {
        var clientId = configuration["LinkedIn:ClientId"]
            ?? throw new InvalidOperationException("LinkedIn:ClientId is not configured.");
        var redirectUri = configuration["LinkedIn:RedirectUri"]
            ?? throw new InvalidOperationException("LinkedIn:RedirectUri is not configured.");

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = "openid profile email"
        };

        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://www.linkedin.com/oauth/v2/authorization?{qs}";
    }

    public async Task<LinkedInUserInfo> ExchangeCodeAsync(string code, string redirectUri)
    {
        var clientId = configuration["LinkedIn:ClientId"]
            ?? throw new InvalidOperationException("LinkedIn:ClientId is not configured.");
        var clientSecret = configuration["LinkedIn:ClientSecret"]
            ?? throw new InvalidOperationException("LinkedIn:ClientSecret is not configured.");

        var client = httpClientFactory.CreateClient();

        // Exchange authorization code for access token
        var tokenResponse = await client.PostAsync(
            "https://www.linkedin.com/oauth/v2/accessToken",
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
            ?? throw new InvalidOperationException("No access_token in LinkedIn response.");

        logger.LogInformation("LinkedIn token exchanged successfully.");

        // Fetch user info using the access token
        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await client.SendAsync(userInfoRequest);
        userInfoResponse.EnsureSuccessStatusCode();

        var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
        logger.LogInformation("LinkedIn userinfo: {Json}", userInfoJson);

        var doc = JsonDocument.Parse(userInfoJson);
        var root = doc.RootElement;

        return new LinkedInUserInfo(
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
