using LinkedInPortfolio.API.Controllers;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LinkedInPortfolio.Tests;

public class SyncControllerTests
{
    [Fact]
    public async Task Sync_WhenScraperSucceeds_Returns200WithSuccessTrue()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();
        var fetchedAt = DateTime.UtcNow;

        mockScraper.Setup(s => s.ScrapeProfileAsync())
            .ReturnsAsync(new ProfileData { Name = "Ahmed", FetchedAt = fetchedAt });

        mockProfileService.Setup(s => s.SaveProfileAsync(It.IsAny<ProfileData>()))
            .Returns(Task.CompletedTask);

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Sync();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SyncResultDto>(ok.Value);
        Assert.True(dto.Success);
        Assert.Equal(fetchedAt, dto.SyncedAt);
        Assert.Equal("Profile synced successfully", dto.Message);
    }

    [Fact]
    public async Task Sync_WhenScraperThrows_Returns500WithSuccessFalse()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();

        mockScraper.Setup(s => s.ScrapeProfileAsync())
            .ThrowsAsync(new InvalidOperationException("Login blocked"));

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        var result = await controller.Sync();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var dto = Assert.IsType<SyncResultDto>(statusResult.Value);
        Assert.False(dto.Success);
        Assert.Contains("Login blocked", dto.Message);
    }

    [Fact]
    public async Task Sync_WhenSucceeds_CallsSaveProfileAsync()
    {
        var mockScraper = new Mock<ILinkedInScraperService>();
        var mockProfileService = new Mock<IProfileService>();
        var profileData = new ProfileData { Name = "Ahmed", FetchedAt = DateTime.UtcNow };

        mockScraper.Setup(s => s.ScrapeProfileAsync()).ReturnsAsync(profileData);
        mockProfileService.Setup(s => s.SaveProfileAsync(It.IsAny<ProfileData>())).Returns(Task.CompletedTask);

        var controller = new SyncController(mockScraper.Object, mockProfileService.Object);
        await controller.Sync();

        mockProfileService.Verify(s => s.SaveProfileAsync(profileData), Times.Once);
    }
}
