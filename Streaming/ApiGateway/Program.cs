using System.Text;
using AuthClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().Build())
    .AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin"));

builder.Host.UseSerilog();
builder.Services.AddControllers();

builder.Services
    .AddGrpcClient<AuthService.AuthServiceClient>(options =>
    {
        options.Address = new Uri(builder.Configuration["ServiceAddresses:AuthService"]);
    })
    .ConfigureChannel(o =>
    {
        o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
        o.Credentials = ChannelCredentials.Insecure;
    });

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
