namespace LinkedInPortfolio.API.DTOs;

public record LinkedInUserInfo(
    string Sub,
    string Name,
    string? GivenName,
    string? FamilyName,
    string? Email,
    bool EmailVerified,
    string? Picture
);
