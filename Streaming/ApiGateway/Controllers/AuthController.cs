using System.Security.Claims;
using ApiGateway.Controllers.Base;
using ApiGateway.Dtos.Auth;
using AuthClientApp;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthGatewayController : BaseGrpcToHttpController
{
    private readonly AuthService.AuthServiceClient _authServiceClient;

    public AuthGatewayController(
        AuthService.AuthServiceClient authServiceClient,
        ILogger<AuthGatewayController> logger) : base(logger)
    {
        _authServiceClient = authServiceClient;
    }

    [HttpPost("register/begin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BeginRegistration([FromBody] RegisterRequest request)
    {
        try
        {
            _logger.LogInformation("Gateway: HTTP BeginRegistration request for email {Email}", request.Email);
            await _authServiceClient.BeginRegistrationAsync(request);
            _logger.LogInformation("Gateway: gRPC BeginRegistration call successful for email {Email}", request.Email);
            return Ok();
        }
        catch (RpcException ex)
        {
            return HandleRpcException(ex, nameof(BeginRegistration));
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex, nameof(BeginRegistration), $"Email: {request?.Email}");
        }
    }

    [HttpPost("register/finish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinishRegistration([FromBody] FinishRequest request)
    {
        try
        {
            _logger.LogInformation("Gateway: HTTP FinishRegistration request for email {Email}", request.Email);
            await _authServiceClient.FinishRegistrationAsync(request);
            _logger.LogInformation("Gateway: gRPC FinishRegistration call successful for email {Email}", request.Email);
            return Ok();
        }
        catch (RpcException ex)
        {
            return HandleRpcException(ex, nameof(FinishRegistration));
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex, nameof(FinishRegistration), $"Email: {request?.Email}");
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(HttpLoginReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Gateway: HTTP Login request for email {Email}", request.Email);
            var reply = await _authServiceClient.LoginAsync(request);
            _logger.LogInformation("Gateway: gRPC Login call successful for email {Email}", request.Email);
            var httpReply = new HttpLoginReply
            {
                JwtToken = reply.JwtToken,
                RefreshToken = reply.RefreshToken,
                ExpiresJwt = reply.ExpiresJwt.ToDateTimeOffset(),
                ExpiresRefresh = reply.ExpiresRefresh.ToDateTimeOffset()
            };
            return Ok(httpReply);
        }
        catch (RpcException ex)
        {
            return HandleRpcException(ex, nameof(Login));
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex, nameof(Login), $"Email: {request?.Email}");
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(HttpLoginReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            _logger.LogInformation("Gateway: HTTP Refresh token request (Token starts with: {JwtStart})",
                request.JwtToken?.Substring(0, Math.Min(10, request.JwtToken.Length)));
            var reply = await _authServiceClient.RefreshAsync(request);
            _logger.LogInformation("Gateway: gRPC Refresh call successful");
            var httpReply = new HttpLoginReply
            {
                JwtToken = reply.JwtToken,
                RefreshToken = reply.RefreshToken,
                ExpiresJwt = reply.ExpiresJwt.ToDateTimeOffset(),
                ExpiresRefresh = reply.ExpiresRefresh.ToDateTimeOffset()
            };
            return Ok(httpReply);
        }
        catch (RpcException ex)
        {
            return HandleRpcException(ex, nameof(Refresh));
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex, nameof(Refresh));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout()
    {
        var emailClaim = User.FindFirst(ClaimTypes.Name);
        if (emailClaim == null || string.IsNullOrEmpty(emailClaim.Value))
        {
            _logger.LogWarning(
                "Gateway (AuthTests): Logout - ClaimTypes.Name (email) is missing or empty in the token for an authenticated user.");
            return Unauthorized(new ProblemDetails
            {
                Title = "User email not found in token.",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The authentication token is missing the required email claim.",
                Instance = HttpContext.Request.Path
            });
        }

        var userEmail = emailClaim.Value;
        _logger.LogInformation("Gateway (AuthTests): HTTP Logout request initiated for user email: {UserEmail}",
            userEmail);

        var grpcLogoutRequest = new LogoutRequest { Email = userEmail };

        try
        {
            await _authServiceClient.LogoutAsync(grpcLogoutRequest);
            _logger.LogInformation("Gateway (AuthTests): gRPC Logout call successful for user email: {UserEmail}",
                userEmail);
            return Ok();
        }
        catch (RpcException ex)
        {
            return HandleRpcException(ex, nameof(Logout));
        }
        catch (Exception ex)
        {
            return HandleUnexpectedError(ex, nameof(Logout), $"UserEmail: {userEmail}");
        }
    }
}