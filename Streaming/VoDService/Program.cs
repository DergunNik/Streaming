using AuthService.Settings;
using CloudinaryDotNet;
using CloudinaryUtils.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoDService.Data;
using VoDService.HealthChecks;
using VoDService.Services;
using VoDService.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.Services
    .Configure<CloudinaryRestrictions>(builder.Configuration)
    .Configure<DbCredentials>(builder.Configuration)
    .AddDbContext<AppDbContext>((serviceProvider, options) =>
    {
        var dbCredentials = serviceProvider.GetRequiredService<IOptions<DbCredentials>>().Value;
        var connectionString = dbCredentials.ToConnectionString();
        options.UseNpgsql(connectionString);
    })
    .AddScoped<VideoService>()
    .AddCloudinaryFromConfig(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"])
    .AddCloudinaryHealthCheck(tags: ["ready"]);

var app = builder.Build();

app.MapGrpcService<VideoService>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();