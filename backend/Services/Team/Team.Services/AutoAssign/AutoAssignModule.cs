using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.AutoAssign;

namespace Team.Services.AutoAssign;

public static class AutoAssignModule
{
    /// <summary>Module AutoAssign (BE-49): áp dụng phương án ghép tự động, Admin chạy lại, xem kết quả. Cần Finalization và <c>AddMatchingClient</c>.</summary>
    public static IServiceCollection AddAutoAssignModule(this IServiceCollection services)
    {
        services.AddScoped<IAutoAssignRepository, AutoAssignRepository>();
        services.AddScoped<UnassignedStudents>();
        services.AddScoped<IAutoAssignService, AutoAssignService>();
        return services;
    }
}
