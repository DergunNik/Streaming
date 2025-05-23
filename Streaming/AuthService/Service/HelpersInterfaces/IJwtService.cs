namespace AuthService.Service.HelpersInterfaces;

public interface IJwtService
{
    Task<(string jwtToken, string refreshToken)> GenerateTokensAsync(string email, string password);
    Task<(string jwtToken, string refreshToken)> RefreshTokenAsync(string jwtToken, string refreshToken);
}