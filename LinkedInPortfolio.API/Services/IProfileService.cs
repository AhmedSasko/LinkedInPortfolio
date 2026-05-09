using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IProfileService
{
    Task<ProfileDto?> GetProfileAsync();
    Task<ProfileStatusDto> GetStatusAsync();
    Task SaveProfileAsync(ProfileData data);
}
