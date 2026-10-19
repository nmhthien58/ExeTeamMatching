// Job Service – khung ban đầu (BE-04). Hangfire + dashboard /hangfire (BE-51).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
