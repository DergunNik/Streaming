using AuthService.Domain.Interfaces;
using AuthService.Persistence;
using AuthService.Services;
using AuthService.Settings;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services
    .Configure<AuthSettings>(builder.Configuration)
    .Configure<DbConnectionSettings>(builder.Configuration)
    .Configure<EncryptionSettings>(builder.Configuration)
    .Configure<ServiceAddresses>(builder.Configuration)
    .AddScoped<IJwtService, JwtService>()
    .AddScoped<AppDbContext>()
    .AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
    .AddScoped<AuthService.Services.AuthService>()
    .AddScoped<IHashService, Argon2HashService>()
    .AddScoped<AuthCredentials>(_ => JsonConvert.DeserializeObject<AuthCredentials>(
        File.ReadAllText("credentials.json")));

var app = builder.Build();

app.MapGrpcService<AuthService.Services.AuthService>();

app.Run();
