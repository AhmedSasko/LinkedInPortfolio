using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInPortfolio.API.Services;

public class SettingsService(AppDbContext db) : ISettingsService
{
    private const string CookiesKey = "LinkedInCookies";

    public async Task<string?> GetLinkedInCookiesAsync()
    {
        var row = await db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == CookiesKey);
        return row?.Value;
    }

    public async Task SetLinkedInCookiesAsync(string cookiesJson)
    {
        var row = await db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == CookiesKey);

        if (row is null)
            db.AppSettings.Add(new AppSetting { Key = CookiesKey, Value = cookiesJson });
        else
            row.Value = cookiesJson;

        await db.SaveChangesAsync();
    }
}
