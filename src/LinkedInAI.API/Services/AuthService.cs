using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LinkedInAI.API.Data;
using LinkedInAI.API.DTOs;
using LinkedInAI.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LinkedInAI.API.Services;

public class AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService> logger) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var strategy = db.Database.CreateExecutionStrategy();
        User user = null!;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            if (await db.Users.AnyAsync(u => u.Email == email))
                throw new InvalidOperationException("Email already registered");

            var isFirst = !await db.Users.AnyAsync();
            user = new User { Email = email, IsAdmin = isFirst };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            logger.LogInformation("User {Email} registered (admin={IsAdmin})", email, isFirst);
        });

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email)
            ?? throw new UnauthorizedAccessException("Invalid email or password");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("This account uses social login. Please sign in with your connected provider.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid email or password");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        logger.LogInformation("User {Email} logged in", email);
        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var token = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (!token.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked");

        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GenerateAuthResponseAsync(token.User);
    }

    public async Task RevokeTokenAsync(string refreshToken, int userId)
    {
        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId)
            ?? throw new InvalidOperationException("Token not found");

        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<AuthResponse> HandleOAuthLoginAsync(OAuthUserInfo userInfo, string? linkedInId = null)
    {
        var email = userInfo.Email?.Trim().ToLowerInvariant();

        User? user = null;

        if (!string.IsNullOrEmpty(linkedInId))
        {
            var account = await db.LinkedInAccounts
                .Include(la => la.User)
                .FirstOrDefaultAsync(la => la.LinkedInId == linkedInId);
            user = account?.User;
        }

        if (user == null && !string.IsNullOrEmpty(email))
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            var isFirst = !await db.Users.AnyAsync();
            user = new User
            {
                Email = email ?? $"{userInfo.Sub}@oauth.local",
                IsAdmin = isFirst
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            logger.LogInformation("OAuth user created: {Email}", user.Email);
        }

        return await GenerateAuthResponseAsync(user);
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var jwt = GenerateJwt(user);
        var refresh = await CreateRefreshTokenAsync(user.Id);
        var expiry = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes());
        return new AuthResponse(jwt, refresh, expiry, user.Id, user.Email, user.IsAdmin);
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured")));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(int userId)
    {
        var tokenBytes = new byte[64];
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.TryParse(config["Jwt:RefreshExpiryDays"], out var days) ? days : 30)
        });

        await db.SaveChangesAsync();
        return token;
    }

    private int GetJwtExpiryMinutes() =>
        int.TryParse(config["Jwt:ExpiryMinutes"], out var min) ? min : 15;
}
