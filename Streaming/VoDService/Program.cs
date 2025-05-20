using AuthService.Settings;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoDService.HealthChecks;
using VoDService.Persistence;
using VoDService.Services;
using VoDService.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

builder.Services.AddGrpc();

builder.Services
    .Configure<CloudinarySettings>(builder.Configuration)
    .Configure<DbCredentials>(builder.Configuration)
    .AddDbContext<AppDbContext>((serviceProvider, options) =>
    {
        var dbCredentials = serviceProvider.GetRequiredService<IOptions<DbCredentials>>().Value;
        var connectionString = dbCredentials.ToConnectionString();
        options.UseNpgsql(connectionString);
    })
    .AddScoped<VideoService>();

builder.Services.AddSingleton<Cloudinary>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CloudinarySettings>>().Value;
    var account = new Account(
        options.CloudName,
        options.ApiKey,
        options.ApiSecret);
    return new Cloudinary(account) { Api = { Secure = true } };
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"])
    .AddCheck<CloudinaryHealthCheck>("cloudinary", tags: ["ready"]);

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