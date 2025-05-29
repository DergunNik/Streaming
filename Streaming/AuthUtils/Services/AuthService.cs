using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using AuthServerApp;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AuthUtils.Services;

/// <summary>
/// Service for handling authentication operations via gRPC AuthService
/// </summary>
/// <remarks>
/// Implements the following token strategy:
/// 1. Checks validity of current tokens
/// 2. Attempts token refresh if needed
/// 3. Falls back to full login if refresh fails
/// </remarks>
public class AuthService : IAuthService
{
    private readonly AuthServerApp.AuthService.AuthServiceClient _client;
    private readonly ILogger<AuthService> _logger;
    private readonly TimeSpan _tokenExpirationThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the AuthService
    /// </summary>
    /// <param name="client">gRPC AuthService client</param>
    /// <param name="logger">Logger instance</param>
    public AuthService(AuthServerApp.AuthService.AuthServiceClient client, ILogger<AuthService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Gets access tokens (either existing valid tokens or new ones)
    /// </summary>
    /// <param name="jwtToken">Current JWT token (optional)</param>
    /// <param name="refreshToken">Current refresh token (optional)</param>
    /// <param name="email">Email for login (required if tokens are invalid)</param>
    /// <param name="password">Password for login (required if tokens are invalid)</param>
    /// <returns>Tuple of (JWT token, Refresh token)</returns>
    /// <exception cref="AuthenticationException">When authentication fails</exception>
    public (string, string) GetAccessToken(string jwtToken, string refreshToken, string email, string password)
    {
        try
        {
            if (AreTokensValid(jwtToken, refreshToken)) { return (jwtToken, refreshToken); }

            if (string.IsNullOrEmpty(jwtToken) || string.IsNullOrEmpty(refreshToken)) { return GetAccessToken(email, password);}
            
            try
            {
                var refreshResponse = _client.Refresh(new RefreshRequest
                {
                    JwtToken = jwtToken,
                    RefreshToken = refreshToken
                });
                    
                return (refreshResponse.JwtToken, refreshResponse.RefreshToken);
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.InvalidArgument)
            {
                _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            }

            return GetAccessToken(email, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token processing");
            throw;
        }
    }

    /// <summary>
    /// Gets new access tokens using credentials
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>Tuple of (JWT token, Refresh token)</returns>
    /// <exception cref="AuthenticationException">When authentication fails</exception>
    public (string, string) GetAccessToken(string email, string password)
    {
        try
        {
            var loginResponse = _client.Login(new LoginRequest
            {
                Email = email,
                Password = password
            });
            
            return (loginResponse.JwtToken, loginResponse.RefreshToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            _logger.LogWarning("Login failed for email: {Email}", email);
            throw new AuthenticationException("Invalid email or password");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            throw;
        }
    }

    /// <summary>
    /// Validates JWT token expiration
    /// </summary>
    /// <param name="jwtToken">JWT token to validate</param>
    /// <param name="refreshToken">Refresh token (unused in current implementation)</param>
    /// <returns>True if token is valid and won't expire within threshold period</returns>
    private bool AreTokensValid(string jwtToken, string refreshToken)
    {
        if (string.IsNullOrEmpty(jwtToken) || string.IsNullOrEmpty(refreshToken))
            return false;

        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHandler.CanReadToken(jwtToken))
                return false;

            var jwtSecurityToken = jwtHandler.ReadJwtToken(jwtToken);
            var jwtExpiration = jwtSecurityToken.ValidTo;
            
            return jwtExpiration >= DateTime.UtcNow.Add(_tokenExpirationThreshold);
        }
        catch
        {
            return false;
        }
    }
}