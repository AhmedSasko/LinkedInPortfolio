using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public interface IProfileService
{
    Task<ProfileDto?> GetLatestAsync(int userId);
    Task<ProfileDto?> GetByIdAsync(int userId, int profileId);
    Task<ProfileDto> UpdateAsync(int userId, UpdateProfileRequest request);
    Task<ProfileDto> SaveScrapedAsync(int userId, ProfileData data);
    Task<ProfileStatusDto> GetStatusAsync(int userId);
    Task<List<ProfileSummaryDto>> GetAllSummariesAsync();
    Task SyncFromLinkedInAsync(int userId, string? name, string? photoUrl);
}
