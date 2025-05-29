namespace AuthUtils.Services;

/// <summary>
/// Interface for authentication service
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets access tokens using either existing tokens or credentials
    /// </summary>
    /// <param name="jwtToken">Current JWT token (optional)</param>
    /// <param name="refreshToken">Current refresh token (optional)</param>
    /// <param name="email">Fallback email for login</param>
    /// <param name="password">Fallback password for login</param>
    /// <returns>Tuple of (JWT token, Refresh token)</returns>
    (string, string) GetAccessToken(string jwtToken, string refreshToken, string email, string password);

    /// <summary>
    /// Gets new access tokens using credentials
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>Tuple of (JWT token, Refresh token)</returns>
    (string, string) GetAccessToken(string email, string password);
}