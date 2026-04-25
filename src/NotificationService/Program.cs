using Azure.Messaging.ServiceBus;
using NotificationService.Hubs;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS — permite frontend-ul să se conecteze la SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:5000",
                builder.Configuration["AllowedOrigin"] ?? "*"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // necesar pentru SignalR WebSocket
    });
});

// SignalR — pentru conexiuni WebSocket cu clienții
builder.Services.AddSignalR();

// Azure Service Bus client — conectare la namespace
var serviceBusConnectionString =
    builder.Configuration["ConnectionStrings:ServiceBus"]
    ?? throw new InvalidOperationException("ServiceBus connection string is missing");

builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnectionString));

// Background service — ascultă mesaje din Service Bus
builder.Services.AddHostedService<ServiceBusListener>();

var app = builder.Build();

app.UseCors();

// Endpoint health check — util pentru Azure App Service
app.MapGet(
    "/health",
    () => Results.Ok(new { status = "healthy", service = "NotificationService" })
);

// Hub SignalR — clienții se conectează la /hubs/comments
app.MapHub<CommentHub>("/hubs/comments");

app.Run();
