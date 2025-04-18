using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Domain.Interfaces;
using AuthService.Domain.Models;
using AuthService.Settings;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

public class JwtService(
    IOptions<AuthSettings> authSettings, 
    IOptions<EncryptionSettings> encryptionSettings,
    ILogger<JwtService> logger, 
    IRepository<User> repository,
    IHashService hashService,
    AuthCredentials credentials
    ) : IJwtService
{
    public async Task<(string jwtToken, string refreshToken)> GenerateTokenAsync(string email, string password)
    {
        logger.LogInformation($"Checking user {email} for jwt token.");
        var user = await repository.FirstOrDefaultAsync(u => u.Email == email) 
            ?? throw new ArgumentException("Wrong email or password.");

        if (((byte)user.BanStatus & (byte)BanStatus.CannotLogin) != 0)
            throw new AuthenticationException("The user is banned.");
        
        if (await hashService.CheckPasswordAsync(password, user.PasswordSalt, user.PasswordHash))
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(credentials.SecretKey)),
        };

        User? user = null;
        
        try
        {
            tokenHandler.ValidateToken(jwtToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtSecurityToken)
                throw new SecurityTokenException("Wrong token type.");

            var idClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                ?? throw new SecurityTokenException("Wrong token claims.");

            var userId = int.Parse(idClaim.Value);
            user = await repository.GetByIdAsync(userId)
                ?? throw new SecurityTokenException("Invalid user id.");

            if (user.RefreshToken != refreshToken || DateTime.UtcNow >= user.RefreshTokenExpiryTime)
                throw new SecurityTokenException("Invalid refresh token.");

            var newJwtToken = GenerateJwtToken(user.Email, user.Id, user.UserRole);
            var newRefreshToken = await GenerateRefreshTokenAsync(user, encryptionSettings.Value.RefreshTokenSize);
            return (newJwtToken, newRefreshToken);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning("Invalid refresh token request. Err: {err}", ex.Message);

            if (user is not null)
            {
                user.RefreshToken = null;
                await repository.UpdateAsync(user);
                await repository.SaveChangesAsync();
            }

            throw new UnauthorizedAccessException("Invalid token.");
        }
    }

    public async Task<string> GenerateRefreshTokenAsync(User user, int tokenSize)
    {
        logger.LogInformation("Generating refresh token.");

        var randomBytes = new byte[tokenSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes); 
        }

        var token = randomBytes.ToString()!;

        user.RefreshToken = token;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(authSettings.Value.RefreshTokenLifetime);
        await repository.UpdateAsync(user);
        await repository.SaveChangesAsync();

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
                    Encoding.UTF8.GetBytes(credentials.SecretKey)),
                SecurityAlgorithms.HmacSha256));
        
        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}

