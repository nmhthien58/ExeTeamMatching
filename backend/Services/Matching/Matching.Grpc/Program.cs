using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using Matching.Grpc.Services;

// Matching Service – chỉ gRPC (HTTP/2), không public qua Gateway nên không cần JWT.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("matching", useAuth: false);
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcServerInterceptor>());

var app = builder.Build();

app.UseServiceDefaults(useAuth: false);
app.MapGrpcService<MatchingGrpcService>();
app.MapGet("/", () => "Matching Service chỉ nhận gRPC (HTTP/2). Hợp đồng: backend/Protos/matching.proto");

app.Run();
