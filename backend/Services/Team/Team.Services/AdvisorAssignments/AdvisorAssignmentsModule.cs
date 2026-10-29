using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;

namespace Team.Services.AdvisorAssignments;

public static class AdvisorAssignmentsModule
{
    public static IServiceCollection AddAdvisorAssignmentsModule(this IServiceCollection services)
    {
        services.AddScoped<ILecturerAssignmentRepository, LecturerAssignmentRepository>();
        services.AddScoped<IMentorAssignmentRepository, MentorAssignmentRepository>();
        services.AddScoped<IAdvisorAssignmentService, AdvisorAssignmentService>();
        return services;
    }
}
