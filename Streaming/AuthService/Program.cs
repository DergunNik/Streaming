using System.Text;
using AuthService.Data;
using AuthService.Services.HelpersImplementations;
using AuthService.Services.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["JwtSettings:Key"];
        var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
        var jwtAudience = builder.Configuration["JwtSettings:Audience"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var emailConfig = builder.Configuration.GetSection("EmailServiceAddress").Get<EmailServiceAddress>();

builder.Services.AddGrpc().AddJsonTranscoding();
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
    .AddScoped<AuthService.Services.AuthService>()
    .AddScoped<IHashService, Argon2HashService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<AuthService.Services.AuthService>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();