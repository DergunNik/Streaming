using System.Net;
using CloudinaryDotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudinaryUtils.HealthChecks;

/// <summary>
///     Health check for verifying Cloudinary API availability.
/// </summary>
public class CloudinaryHealthCheck : IHealthCheck
{
    private readonly Cloudinary _cloudinary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CloudinaryHealthCheck" /> class.
    /// </summary>
    /// <param name="cloudinary">The Cloudinary client instance.</param>
    public CloudinaryHealthCheck(Cloudinary cloudinary)
    {
        _cloudinary = cloudinary;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
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