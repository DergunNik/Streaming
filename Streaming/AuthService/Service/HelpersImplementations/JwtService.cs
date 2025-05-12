using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Models;
using AuthService.Persistence;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Service.HelpersImplementations;

public class JwtService(
    IOptions<AuthSettings> authSettings,
    IOptions<EncryptionSettings> encryptionSettings,
    ILogger<JwtService> logger,
    IRepository<User> userRepository,
    IRepository<RefreshToken> refreshTokenRepository,
    IHashService hashService,
    JwtSettings credentials
) : IJwtService
{
    public async Task<(string jwtToken, string refreshToken)> GenerateTokenAsync(string email, string password)
    {
        logger.LogInformation($"Checking user {email} for jwt token.");
        var user = await userRepository.FirstOrDefaultAsync(u => u.Email == email)
                   ?? throw new ArgumentException("Wrong email or password.");

        if (((byte)user.BanStatus & (byte)BanStatus.CannotLogin) != 0)
            throw new AuthenticationException("The user is banned.");

        if (!await hashService.CheckPasswordAsync(password, user.PasswordHash)) // теперь только один аргумент
            throw new ArgumentException("Wrong email or password.");

        var jwtToken = GenerateJwtToken(email, user.Id, user.UserRole);
        var refreshToken = await GenerateRefreshTokenAsync(user, encryptionSettings.Value.RefreshTokenSize);

        return (jwtToken, refreshToken);
    }

    public async Task<(string jwtToken, string refreshToken)> RefreshTokenAsync(string jwtToken, string refreshToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        if (!tokenHandler.CanReadToken(jwtToken))
            throw new Exception("Cannot read token.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(credentials.Key)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        User? user;

        try
        {
            tokenHandler.ValidateToken(jwtToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtSecurityToken)
                throw new SecurityTokenException("Wrong token type.");

            var idClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                          ?? throw new SecurityTokenException("Wrong token claims.");

            var userId = int.Parse(idClaim.Value);
            user = await userRepository.GetByIdAsync(userId)
                   ?? throw new SecurityTokenException("Invalid user id.");

            var dbRefreshToken = await refreshTokenRepository.FirstOrDefaultAsync(
                t => t.UserId == user.Id && t.Token == refreshToken);

            if (dbRefreshToken == null || DateTime.UtcNow >= dbRefreshToken.ExpiresOnUtc)
                throw new SecurityTokenException("Invalid or expired refresh token.");

            await refreshTokenRepository.DeleteAsync(dbRefreshToken);
            await refreshTokenRepository.SaveChangesAsync();

            var newJwtToken = GenerateJwtToken(user.Email, user.Id, user.UserRole);
            var newRefreshToken = await GenerateRefreshTokenAsync(user, encryptionSettings.Value.RefreshTokenSize);
            return (newJwtToken, newRefreshToken);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning("Invalid refresh token request. Err: {err}", ex.Message);
            throw new UnauthorizedAccessException("Invalid token.");
        }
    }

    private async Task<string> GenerateRefreshTokenAsync(User user, int tokenSize)
    {
        logger.LogInformation("Generating refresh token.");

        var randomBytes = new byte[tokenSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var token = Convert.ToBase64String(randomBytes);

        var refreshToken = new RefreshToken
        {
            Token = token,
            ExpiresOnUtc = DateTime.UtcNow.Add(authSettings.Value.RefreshTokenLifetime),
            UserId = user.Id,
            User = user
        };

        await refreshTokenRepository.AddAsync(refreshToken);
        await refreshTokenRepository.SaveChangesAsync();

        return token;
    }

    private string GenerateJwtToken(string email, int id, UserRole userRole)
    {
        logger.LogInformation("Generating JwtToken to {email}", email);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Role, userRole.ToString())
        };
        var jwtToken = new JwtSecurityToken(
            expires: DateTime.UtcNow.Add(authSettings.Value.AccessTokenLifetime),
            claims: claims,
            signingCredentials:
            new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(credentials.Key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}