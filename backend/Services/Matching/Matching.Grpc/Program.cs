using Matching.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<MatchingGrpcService>();
app.MapHealthChecks("/health");
app.MapGet("/", () => "Matching Service chỉ nhận gRPC (HTTP/2). Hợp đồng: backend/Protos/matching.proto");

app.Run();
