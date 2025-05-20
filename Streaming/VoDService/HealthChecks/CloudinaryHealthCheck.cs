using System.Net;
using CloudinaryDotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VoDService.HealthChecks;

public class CloudinaryHealthCheck : IHealthCheck
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryHealthCheck(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _cloudinary.PingAsync(cancellationToken);
            return result.StatusCode == HttpStatusCode.OK 
                ? HealthCheckResult.Healthy() 
                : HealthCheckResult.Unhealthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}