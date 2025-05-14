using System.Text;
using ApiGateway.Settings;
using AuthClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ServiceConfig = Grpc.Net.Client.Configuration.ServiceConfig;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var jwtKey = builder.Configuration["JwtSettings:Key"];
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero 
        };
    });

builder.Services.Configure<ServiceAddresses>(builder.Configuration.GetSection("ServiceAddresses"));
var serviceAddresses = builder.Configuration.GetSection("ServiceAddresses").Get<ServiceAddresses>();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().Build())
    .AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin"));

builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(serviceAddresses.AuthService.GetHttpUrl() + "/health/live"),
        name: "auth-service",
        tags: ["ready"]);


builder.Services
    .AddGrpcClient<AuthService.AuthServiceClient>(options =>
    {
        options.Address = new Uri(serviceAddresses.AuthService.GetGrpcUrl());
    })
    .ConfigureChannel(o =>
    {
        o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
        o.Credentials = ChannelCredentials.Insecure;
    });


var app = builder.Build();

app.UseHttpsRedirection()
    .UseRouting()
    .UseHttpLogging()
    .UseAuthentication()
    .UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();
