using BuildingBlocks.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Projects;
using Team.Services.Academic;

namespace Team.Services.Projects;

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddHttpClient<IProjectSemesterClient, ProjectSemesterClient>(http =>
                http.BaseAddress = new Uri(configuration["Academic:BaseUrl"] ?? "http://localhost:5002"))
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<ServiceTokenHandler>();
        return services;
    }
}
