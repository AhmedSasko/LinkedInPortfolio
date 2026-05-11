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
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
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
