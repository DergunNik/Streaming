using AuthService.Persistence;
using AuthService.Service.HelpersImplementations;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Newtonsoft.Json;

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

var emailConfig = builder.Configuration.GetSection("EmailServiceAddress").Get<EmailServiceAddress>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddUrlGroup(
        new Uri(emailConfig.GetEmailHttpUrl() + "/health/live"), 
        name: "EmailService", 
        tags: ["ready"]);

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