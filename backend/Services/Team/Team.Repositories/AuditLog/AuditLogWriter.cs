using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Team.Repositories.Context;

namespace Team.Repositories.AuditLog;

/// <summary>Người thực hiện thay đổi; <see cref="AccountId"/> null = hệ thống / job.</summary>
public sealed record AuditActor(Guid? AccountId, string Role)
{
    public static readonly AuditActor System = new(null, "System");
}

/// <summary>
/// Ghi nhật ký thay đổi nhóm (rule 10) vào <c>team.audit_logs</c>, cùng transaction với thay đổi nghiệp vụ.
/// Mọi module của Team Service dùng chung lớp này.
/// </summary>
public sealed class AuditLogWriter(TeamDbContext db)
{
    public void Add(Guid? teamId, string entityType, Guid entityId, string action, AuditActor actor,
        object? oldValues = null, object? newValues = null, string? reason = null) =>
        db.Add(new AuditLogEntry
        {
            TeamId = teamId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ActorAccountId = actor.AccountId,
            ActorRole = actor.Role,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, EventEnvelope.JsonOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, EventEnvelope.JsonOptions),
            Reason = reason,
            CorrelationId = CorrelationContext.Id,
        });
}
