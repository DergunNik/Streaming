using AuthUtils.Settings;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthUtils.DependencyInjection;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers gRPC client for AuthService with configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="addressSectionName">Configuration section name for service address</param>
    /// <param name="credentialsSectionName">Configuration section name for credentials</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddAuthFromConfig(
        this IServiceCollection services,
        IConfiguration configuration,
        string addressSectionName = "AuthServiceAddress",
        string credentialsSectionName = "AuthServiceAddress")
    {
        services.Configure<AuthServiceAddress>(
            configuration.GetSection(addressSectionName));
        services.Configure<AuthCredentials>(
            configuration.GetSection(credentialsSectionName));
        var authAddress = configuration.GetSection(addressSectionName).Get<AuthServiceAddress>();

        services.AddGrpcClient<AuthServerApp.AuthService.AuthServiceClient>(options =>
            {
                options.Address = new Uri(authAddress.Url);
            })
            .ConfigureChannel(o =>
            {
                o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
                o.Credentials = ChannelCredentials.Insecure;
            });
        return services;
    }
    
    /// <summary>
    /// Adds health check for AuthService
    /// </summary>
    /// <param name="builder">Health checks builder</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="addressSectionName">Configuration section name for service address</param>
    /// <param name="name">Health check name</param>
    /// <param name="tags">Health check tags</param>
    /// <returns>Configured health checks builder</returns>
    public static IHealthChecksBuilder AddAuthHealthCheck(
        this IHealthChecksBuilder builder,
        IConfiguration configuration,
        string addressSectionName = "AuthServiceAddress",
        string name = "authService",
        params string[] tags)
    {
        var authAddress = configuration.GetSection(addressSectionName).Get<AuthServiceAddress>();
        builder.AddUrlGroup(
            uri: new Uri(new Uri(authAddress.Url), "/health/live"),
            name: name,
            tags: tags);
        return builder;
    }
}