// Notification Service – khung ban đầu (BE-04). API đọc thông báo + BackgroundService consume Kafka (BE-40..42).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
