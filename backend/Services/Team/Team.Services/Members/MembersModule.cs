using BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Team.Repositories.Members;

namespace Team.Services.Members;

public static class MembersModule
{
    /// <summary>Module Members (BE-30): xóa thành viên, rời nhóm, chuyển trưởng nhóm; vào / rời nhóm dùng chung.</summary>
    public static IServiceCollection AddMembersModule(this IServiceCollection services)
    {
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<MembershipService>();
        services.AddScoped<StudentTeamStatusService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddEventHandler<StudentProfileUpdatedHandler>(Topics.Student, "StudentProfileUpdated");
        return services;
    }
}
