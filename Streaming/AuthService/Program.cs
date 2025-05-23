using AuthService.Data;
using AuthService.Service.HelpersImplementations;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

var emailConfig = builder.Configuration.GetSection("EmailServiceAddress").Get<EmailServiceAddress>();

builder.Services.AddGrpc();
builder.Services.AddGrpcClient<EmailService.EmailServiceClient>(options =>
    {
        options.Address = new Uri(emailConfig.GetEmailGrpcUrl());
    })
    .ConfigureChannel(o =>
    {
        o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
        o.Credentials = ChannelCredentials.Insecure;
    });

builder.Services
    .Configure<AuthSettings>(builder.Configuration)
    .Configure<DbCredentials>(builder.Configuration)
    .Configure<EncryptionSettings>(builder.Configuration)
    .Configure<EmailServiceAddress>(builder.Configuration)
    .Configure<JwtSettings>(builder.Configuration)
    .AddScoped<IJwtService, JwtService>()
    .AddScoped<AppDbContext>()
    .AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
    .AddScoped<AuthService.Service.AuthService>()
    .AddScoped<IHashService, Argon2HashService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"])
    .AddUrlGroup(
        new Uri(emailConfig.GetEmailHttpUrl() + "/health/live"), 
        name: "EmailService", 
        tags: ["ready"]);

var app = builder.Build();

app.MapGrpcService<AuthService.Service.AuthService>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();