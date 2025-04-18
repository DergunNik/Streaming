using EmailService.Services;
using EmailService.Settings;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services
    .AddScoped<EmailService.Services.EmailService>()
    .AddScoped<EmailCredentials>(_ => 
        JsonConvert.DeserializeObject<EmailCredentials>(
                File.ReadAllText("credentials.json")));

var app = builder.Build();

app.MapGrpcService<EmailService.Services.EmailService>();

app.Run();