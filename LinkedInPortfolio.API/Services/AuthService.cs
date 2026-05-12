using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LinkedInPortfolio.API.Data;
using LinkedInPortfolio.API.DTOs;
using LinkedInPortfolio.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LinkedInPortfolio.API.Services;

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponseDto?> RegisterAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();

        using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        if (await db.Users.AnyAsync(u => u.Email == normalised))
        {
            await transaction.RollbackAsync();
            return null;
        }

        var isFirst = !await db.Users.AnyAsync();
        var user = new User
        {
            Email = normalised,
            IsAdmin = isFirst,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new AuthResponseDto(GenerateToken(user));
    }

    public async Task<AuthResponseDto?> LoginAsync(string email, string password)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);
        if (user is null || user.PasswordHash is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash!, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        return new AuthResponseDto(GenerateToken(user));
    }

    public async Task<AuthResponseDto> HandleGoogleLoginAsync(OAuthUserInfo userInfo)
    {
        // 1. Try find user by LinkedInId (reused field for Google sub)
        var user = await db.Users.FirstOrDefaultAsync(u => u.LinkedInId == userInfo.Sub);

        // 2. If not found, try find by email
        if (user is null && !string.IsNullOrEmpty(userInfo.Email))
        {
            var normalised = userInfo.Email.Trim().ToLowerInvariant();
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalised);

            // 3. Found by email but no OAuth sub set — link the account
            if (user is not null && user.LinkedInId is null)
            {
                user.LinkedInId = userInfo.Sub;
                await db.SaveChangesAsync();
            }
        }

        // 4. Not found at all — create new user
        if (user is null)
        {
            var isFirst = !await db.Users.AnyAsync();
            var email = string.IsNullOrEmpty(userInfo.Email)
                ? $"{userInfo.Sub}@google.local"
                : userInfo.Email.Trim().ToLowerInvariant();

            user = new User
            {
                Email = email,
                PasswordHash = null,
                LinkedInId = userInfo.Sub,
                IsAdmin = isFirst,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        return new AuthResponseDto(GenerateToken(user));
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower())
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:ExpiryDays")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
