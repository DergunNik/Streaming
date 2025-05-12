using EmailService.Settings;
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
    .Configure<EmailCredentials>(builder.Configuration)
    .AddScoped<EmailService.Service.EmailService>();

var app = builder.Build();

app.MapGrpcService<EmailService.Service.EmailService>();

app.Run();