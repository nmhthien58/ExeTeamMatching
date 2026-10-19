// API Gateway – khung ban đầu (BE-04). YARP và JWT thêm ở BE-09.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
