using System.Text;
using System.Text.Json;
using AccountService.Data;
using AccountService.Settings;
using CloudinaryUtils.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services
    .AddHttpContextAccessor()
    .Configure<CloudinaryRestrictions>(builder.Configuration.GetSection("CloudinaryRestrictions"))
    .Configure<ContentRestrictions>(builder.Configuration.GetSection("ContentRestrictions"))
    .Configure<DbCredentials>(builder.Configuration.GetSection("DbCredentials"))
    .AddDbContext<AppDbContext>((serviceProvider, options) =>
    {
        var dbCredentials = serviceProvider.GetRequiredService<IOptions<DbCredentials>>().Value;
        if (string.IsNullOrEmpty(dbCredentials.Host) || 
            string.IsNullOrEmpty(dbCredentials.Password) || 
            string.IsNullOrEmpty(dbCredentials.Port) || 
            string.IsNullOrEmpty(dbCredentials.Db) ||
            string.IsNullOrEmpty(dbCredentials.User))
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            const string errorMessage = "dbCredentials are required";
            logger.LogCritical(errorMessage);
        }
        var connectionString = dbCredentials.ToConnectionString();
        options.UseNpgsql(connectionString);
    })
    .AddScoped<AccountService.Services.AccountService>()
    .AddCloudinaryFromConfig(builder.Configuration, "CloudinarySettings");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"]);
    // .AddCloudinaryHealthCheck(tags: ["ready"]);


var app = builder.Build();

app.UseAuthorization();
app.UseAuthorization();

app.MapGrpcService<AccountService.Services.AccountService>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();
