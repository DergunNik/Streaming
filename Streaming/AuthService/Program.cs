using AuthService.Persistence;
using AuthService.Service.HelpersImplementations;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7070, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services
    .Configure<AuthSettings>(builder.Configuration)
    .Configure<DbConnectionSettings>(builder.Configuration)
    .Configure<EncryptionSettings>(builder.Configuration)
    .Configure<ServiceAddresses>(builder.Configuration)
    .Configure<JwtSettings>(builder.Configuration)
    .AddScoped<IJwtService, JwtService>()
    .AddScoped<AppDbContext>()
    .AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
    .AddScoped<AuthService.Service.AuthService>()
    .AddScoped<IHashService, Argon2HashService>()
    .AddGrpcClient<EmailService.EmailServiceClient>(options =>
    {
        options.Address = new Uri(builder.Configuration["ServiceAddresses:EmailService"]);
    })
    .ConfigureChannel(o =>
    {
        o.ServiceConfig = new ServiceConfig { LoadBalancingConfigs = { new RoundRobinConfig() } };
        o.Credentials = ChannelCredentials.Insecure;
    });

var app = builder.Build();

app.MapGrpcService<AuthService.Service.AuthService>();

app.Run();