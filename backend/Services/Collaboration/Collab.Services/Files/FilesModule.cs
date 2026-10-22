using Collab.Repositories.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collab.Services.Files;

public static class FilesModule
{
    /// <summary>Module Files (BE-65) và quyền xem kho EXE101 (BE-77).</summary>
    public static IServiceCollection AddFilesModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IWorkspaceLookup, WorkspaceLookup>();
        services.AddScoped<IWorkspaceReader, WorkspaceReader>();
        services.AddScoped<IFileService, FileService>();
        return services;
    }
}
