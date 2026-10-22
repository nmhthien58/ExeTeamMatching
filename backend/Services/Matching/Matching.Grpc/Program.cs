using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using Matching.Grpc.Services;
using Matching.Services;

// Matching Service (BE-34) – chỉ gRPC (HTTP/2), không public qua Gateway nên không cần JWT.
// Read model Redis dựng từ Kafka (BE-35); không có SQL database.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("matching", useAuth: false);
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcServerInterceptor>());
builder.Services.AddMatchingServices(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults(useAuth: false);
app.MapGrpcService<MatchingGrpcService>();
app.MapGet("/", () => "Matching Service chỉ nhận gRPC (HTTP/2). Hợp đồng: backend/Protos/matching.proto");

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
