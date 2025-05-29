using System.Text;
using AuthUtils.DependencyInjection;
using AuthUtils.Services;
using CloudinaryUtils.DependencyInjection;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using LiveService.Data;
using LiveService.Hubs;
using LiveService.Services;
using LiveService.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/streamchathub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var redisConnectionString = builder.Configuration.GetConnectionString("RedisSignalRBackplane");
builder.Services.AddSignalR(options => { options.EnableDetailedErrors = builder.Environment.IsDevelopment(); })
    .AddStackExchangeRedis(redisConnectionString, options => { options.Configuration.ChannelPrefix = "LiveService"; });

var vodConfig = builder.Configuration.GetSection("VodServiceAddress").Get<VodServiceAddress>();

builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services.AddGrpcClient<VoD.VideoService.VideoServiceClient>(options =>
    {
        options.Address = new Uri(vodConfig.Url);
    })
    .ConfigureChannel(o =>
    {
        o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
        o.Credentials = ChannelCredentials.Insecure;
    });

builder.Services
    .Configure<DbCredentials>(builder.Configuration.GetSection("DbCredentials"))
    .Configure<ContentRestrictions>(builder.Configuration.GetSection("ContentRestrictions"))
    .Configure<CloudinaryRestrictions>(builder.Configuration.GetSection("CloudinaryRestrictions"))
    .AddHttpContextAccessor()
    .AddDbContext<AppDbContext>((serviceProvider, options) =>
    {
        var dbCredentials = serviceProvider.GetRequiredService<IOptions<DbCredentials>>().Value;
        var connectionString = dbCredentials.ToConnectionString();
        options.UseNpgsql(connectionString);
    })
    .AddCloudinaryFromConfig(builder.Configuration, "CloudinarySettings")
    .AddScoped<StreamService>()
    .AddScoped<ChatHub>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"])
    .AddUrlGroup(
        uri: new Uri(new Uri(vodConfig.Url), "/health/live"),
        name: "vodService",
        tags: ["archive"]);
// .AddCloudinaryHealthCheck(tags: ["ready"]);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<StreamService>();
app.MapHub<ChatHub>("/streamchathub");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
});

app.Run();