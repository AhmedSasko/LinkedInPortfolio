using LinkedInPortfolio.API.DTOs;

namespace LinkedInPortfolio.API.Services;

public interface IProfileService
{
    Task<List<ProfileSummaryDto>> GetAllSummariesAsync();
    Task<ProfileDto?> GetProfileAsync();
    Task<ProfileDto?> GetProfileByIdAsync(int id);
    Task<ProfileStatusDto> GetStatusAsync();
    Task SaveProfileAsync(ProfileData data);
}
