using BuildingBlocks.Observability;
using ExeTeamMatching.Protos.Matching.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Team.Services.Matching;

public static class MatchingModule
{
    /// <summary>gRPC client gọi Matching (<c>Matching:BaseUrl</c>, HTTP/2 không TLS trong Docker); dùng chung cho Finalization, AutoAssign, gợi ý.</summary>
    public static IServiceCollection AddMatchingClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<GrpcClientCorrelationInterceptor>();
        services.AddGrpcClient<MatchingService.MatchingServiceClient>(o =>
                o.Address = new Uri(configuration["Matching:BaseUrl"] ?? "http://localhost:5004"))
            .AddInterceptor<GrpcClientCorrelationInterceptor>();
        services.AddScoped<IMatchingClient, MatchingClient>();
        return services;
    }
}
