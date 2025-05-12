using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers.Base;

public abstract class BaseGrpcToHttpController : ControllerBase
{
    protected readonly ILogger<AuthGatewayController> _logger;

    protected BaseGrpcToHttpController(ILogger<AuthGatewayController> logger)
    {
        _logger = logger;
    }
    
    protected IActionResult HandleRpcException(RpcException ex, string operationName)
    {
        _logger.LogError(ex, "Gateway: RpcException during {Operation}: {StatusCode} - {Detail}", operationName, ex.StatusCode, ex.Status.Detail);
        
        var problemDetails = new ProblemDetails
        {
            Instance = HttpContext.Request.Path
        };

        switch (ex.StatusCode)
        {
            case Grpc.Core.StatusCode.Unauthenticated:
                problemDetails.Title = "Authentication failed.";
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                problemDetails.Detail = ex.Status.Detail;
                break;
            case Grpc.Core.StatusCode.PermissionDenied:
                problemDetails.Title = "Permission denied.";
                problemDetails.Status = StatusCodes.Status403Forbidden;
                problemDetails.Detail = ex.Status.Detail;
                break;
            case Grpc.Core.StatusCode.InvalidArgument:
                problemDetails.Title = "Invalid argument provided.";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Detail = ex.Status.Detail;
                break;
            case Grpc.Core.StatusCode.NotFound:
                problemDetails.Title = "Resource not found.";
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Detail = ex.Status.Detail;
                break;
            case Grpc.Core.StatusCode.AlreadyExists:
                problemDetails.Title = "Resource already exists.";
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Detail = ex.Status.Detail;
                break;
            case Grpc.Core.StatusCode.Internal:
                problemDetails.Title = "Auth service internal error.";
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Detail = "An internal error occurred in the authentication service. Please try again later.";
                break;
            default:
                problemDetails.Title = "Error communicating with auth service.";
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Detail = $"The authentication service returned an unexpected error (Code: {ex.StatusCode}).";
                break;
        }
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
    
    protected IActionResult HandleUnexpectedError(Exception ex, string operationName, string? contextualInfoForLog = null)
    {
        var logMessage = $"Gateway: Unexpected error during {operationName}";
        if (!string.IsNullOrEmpty(contextualInfoForLog))
        {
            logMessage += $" (Context: {contextualInfoForLog})";
        }
            
        _logger.LogError(ex, "{LogMessage}", logMessage);

        var problemDetails = new ProblemDetails
        {
            Title = "Gateway internal error",
            Detail = "An unexpected error occurred within the API gateway. Please try again later.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = HttpContext?.Request?.Path
        };
        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status500InternalServerError };
    }
}