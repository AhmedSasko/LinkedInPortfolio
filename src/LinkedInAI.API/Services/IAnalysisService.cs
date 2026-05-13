using LinkedInAI.API.DTOs;

namespace LinkedInAI.API.Services;

public interface IAnalysisService
{
    Task<AnalysisResultDto> RequestAnalysisAsync(int userId, string analysisType = "full");
    Task<AnalysisResultDto?> GetLatestAsync(int userId);
    Task<AnalysisResultDto?> GetByIdAsync(int userId, int analysisId);
    Task<AnalysisStatusDto> GetStatusAsync(int userId);
    Task<List<AnalysisSummaryDto>> GetAllAsync();
    Task UpdateResultAsync(string messageId, string status, string? result, int? score, string? error);
}
