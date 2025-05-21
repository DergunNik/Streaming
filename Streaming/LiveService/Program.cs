using LiveService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSignalR();

var app = builder.Build();

app.MapGrpcService<StreamService>();

app.Run();