using EmailService.Settings;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;

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

builder.Services.AddHealthChecks();
builder.Services.AddGrpc();
builder.Services
    .Configure<EmailCredentials>(builder.Configuration)
    .AddScoped<EmailService.Service.EmailService>();


var app = builder.Build();

app.MapGrpcService<EmailService.Service.EmailService>();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();