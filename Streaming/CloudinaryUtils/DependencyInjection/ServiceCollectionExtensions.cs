using CloudinaryDotNet;
using CloudinaryUtils.HealthChecks;
using CloudinaryUtils.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CloudinaryUtils.DependencyInjection;

/// <summary>
/// Extension methods for registering Cloudinary and related health checks in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Cloudinary as a singleton in the DI container using settings from the configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="sectionName">The name of the configuration section (default: "Cloudinary").</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddCloudinaryFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Cloudinary")
    {
        services.Configure<CloudinarySettings>(
            configuration.GetSection(sectionName));
        services.AddSingleton<Cloudinary>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CloudinarySettings>>().Value;
            var account = new Account(
                options.CloudName,
                options.ApiKey,
                options.ApiSecret);
            return new Cloudinary(account) { Api = { Secure = true } };
        });
        return services;
    }

    /// <summary>
    /// Adds a Cloudinary health check to the health check builder.
    /// </summary>
    /// <param name="builder">The health checks builder (e.g., services.AddHealthChecks()).</param>
    /// <param name="name">The name for the health check (default: "cloudinary").</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <returns>The updated health checks builder.</returns>
    public static IHealthChecksBuilder AddCloudinaryHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "cloudinary",
        params string[] tags)
    {
        if (tags is { Length: > 0 })
        {
            builder.AddCheck<CloudinaryHealthCheck>(name, tags: tags);
        }
        else
        {
            builder.AddCheck<CloudinaryHealthCheck>(name);
        }
        return builder;
    }
}