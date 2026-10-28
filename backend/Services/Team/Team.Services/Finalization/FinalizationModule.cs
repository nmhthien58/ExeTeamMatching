using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Finalization;

namespace Team.Services.Finalization;

public static class FinalizationModule
{
    /// <summary>Module Finalization (BE-48): chốt nhóm khi đóng đợt, Admin chốt / mở lại, thêm / chuyển thành viên. Cần <c>AddMatchingClient</c>.</summary>
    public static IServiceCollection AddFinalizationModule(this IServiceCollection services)
    {
        services.AddScoped<IFinalizationRepository, FinalizationRepository>();
        services.AddScoped<TeamFinalizer>();
        services.AddScoped<IFinalizationService, FinalizationService>();
        return services;
    }
}
