namespace LinkedInPortfolio.API.DTOs;

public record OAuthUserInfo(
    string Sub,
    string Name,
    string? GivenName,
    string? FamilyName,
    string? Email,
    bool EmailVerified,
    string? Picture
);
