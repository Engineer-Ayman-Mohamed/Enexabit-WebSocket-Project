using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EnexabitWebSocketProject.App.Services;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public TokenService(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }
    
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("displayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(_config["Jwt:ExpiryInMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(int userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenString = Convert.ToBase64String(tokenBytes);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _db.SaveChangesAsync();
        return tokenString;
    }

    public async Task<(string accessToken, string refreshToken)?> RotateRefreshTokenAsync(string oldRefreshToken)
    {
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == oldRefreshToken);

        if (stored is null) return null;
        if (stored.ExpiresAt < DateTime.UtcNow) return null;
        if (stored.RevokedAt is not null)
        {
            await RevokeAllUserTokensAsync(stored.UserId);
            return null;
        }

        stored.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = await GenerateRefreshTokenAsync(stored.UserId);
        stored.ReplacedByToken = newRefreshToken;

        await _db.SaveChangesAsync();

        return (GenerateAccessToken(stored.User), newRefreshToken);
    }

    public async Task RevokeAllUserTokensAsync(int userId)
    {
        var active = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var rt in active)
            rt.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
