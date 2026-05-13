using LinkedInAI.API.DTOs;
using Microsoft.AspNetCore.Http;

namespace LinkedInAI.API.Services;

public interface IResumeService
{
    Task<ResumeUploadResponse> UploadAsync(int userId, IFormFile file);
    Task<List<ResumeDto>> GetUploadsAsync(int userId);
    Task<ResumeDto?> GetByIdAsync(int userId, int resumeId);
    Task<ProfileDto> ImportToProfileAsync(int userId, int resumeId);
    Task UpdateParseResultAsync(int resumeId, string status, string? parsedData, string? error);
}
