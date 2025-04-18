namespace AuthService.Domain.Interfaces;

public interface IJwtService
{
    Task<(string jwtToken, string refreshToken)> GenerateTokenAsync(string email, string password);
    Task<(string jwtToken, string refreshToken)> RefreshTokenAsync(string jwtToken, string refreshToken);
}