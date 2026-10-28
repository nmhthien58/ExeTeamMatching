using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collab.Services.Checkpoints;

public static class CheckpointsModule
{
    public static IServiceCollection AddCheckpointsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<Collab.Repositories.Checkpoints.ICheckpointRepository, Collab.Repositories.Checkpoints.CheckpointRepository>();
        services.AddScoped<ICheckpointService, CheckpointService>();
        services.AddHttpClient<IAcademicSemesterClient, AcademicSemesterClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Academic:BaseUrl"] ?? "http://localhost:5002");
        });
        return services;
    }
}
