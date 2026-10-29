using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Projects;

namespace Team.Services.Projects;

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }
}
