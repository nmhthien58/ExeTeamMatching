using Collab.Repositories.Tasks;
using Collab.Services.Files;
using Microsoft.Extensions.DependencyInjection;

namespace Collab.Services.Tasks;

public static class TasksModule
{
    /// <summary>Module Tasks (BE-70): bảng việc, bài nộp, duyệt bài. Cần <see cref="FilesModule.AddFilesModule"/>.</summary>
    public static IServiceCollection AddTasksModule(this IServiceCollection services)
    {
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<SubmissionReader>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IDeliverableService, DeliverableService>();
        return services;
    }
}
