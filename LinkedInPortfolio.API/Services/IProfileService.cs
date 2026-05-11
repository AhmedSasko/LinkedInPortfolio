using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;

namespace LinkedInPortfolio.API.Services;

public interface IProfileService
{
    Task<ProfileSnapshot> SaveProfileAsync(int userId, ProfileData data);
    Task<ProfileSnapshot?> GetLatestAsync(int userId);
    Task<ProfileSnapshot?> GetByIdAsync(int userId, int snapshotId);
    Task<List<ProfileSummaryDto>> GetAllSummariesAsync();
    Task<SyncStatusDto> GetStatusAsync(int userId);
}
