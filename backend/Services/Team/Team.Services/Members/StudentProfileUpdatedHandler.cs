using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Team.Repositories.Context;
using Team.Repositories.Members;

namespace Team.Services.Members;

public sealed record StudentProfileUpdatedPayload(
    Guid StudentId,
    string? FullName,
    int? SpecializationId,
    int MajorId = 0);

public sealed class StudentProfileUpdatedHandler(
    TeamDbContext db,
    ILogger<StudentProfileUpdatedHandler> logger) : IIntegrationEventHandler
{
    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.GetPayload<StudentProfileUpdatedPayload>();
        var members = await db.Set<TeamMember>()
            .Where(m => m.StudentId == payload.StudentId)
            .ToListAsync(cancellationToken);

        if (members.Count == 0) return;

        foreach (var member in members)
        {
            if (payload.SpecializationId.HasValue)
            {
                member.SpecializationId = payload.SpecializationId.Value;
            }
            if (payload.MajorId > 0)
            {
                member.MajorId = payload.MajorId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Đã cập nhật chuyên ngành cho sinh viên {StudentId} trong {Count} nhóm.", payload.StudentId, members.Count);
    }
}
