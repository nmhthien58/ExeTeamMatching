using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Rounds;

public interface IRoundRepository
{
    /// <summary>Lọc tùy chọn theo học kỳ, môn, trạng thái; sắp theo giờ mở.</summary>
    Task<IReadOnlyList<RegistrationRound>> ListAsync(Guid? semesterId, string? courseCode, string? status, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<RegistrationRound?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Khoảng [opensAt, deadlineAt) chồng lên đợt khác cùng học kỳ, môn, loại (EXCLUDE <c>ex_registration_rounds_overlap</c>).</summary>
    Task<bool> OverlapsAsync(RegistrationRound round, CancellationToken ct);

    /// <summary>Đã có đợt khác cùng môn và loại đang mở (ở bất kỳ học kỳ nào).</summary>
    Task<bool> OtherOpenExistsAsync(RegistrationRound round, CancellationToken ct);

    void Add(RegistrationRound round);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.registration</c>, key = id đợt); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid roundId, TPayload payload);

    Task SaveAsync(CancellationToken ct);
}

public sealed class RoundRepository(AcademicDbContext db) : IRoundRepository
{
    public async Task<IReadOnlyList<RegistrationRound>> ListAsync(Guid? semesterId, string? courseCode, string? status, CancellationToken ct)
    {
        var query = db.RegistrationRounds.AsNoTracking();
        if (semesterId is { } id)
        {
            query = query.Where(r => r.SemesterId == id);
        }
        if (courseCode is not null)
        {
            query = query.Where(r => r.CourseCode == courseCode);
        }
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }
        return await query.OrderBy(r => r.OpensAt).ThenBy(r => r.CourseCode).ToListAsync(ct);
    }

    public Task<RegistrationRound?> FindAsync(Guid id, CancellationToken ct) => db.RegistrationRounds.SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> OverlapsAsync(RegistrationRound round, CancellationToken ct) =>
        db.RegistrationRounds.AnyAsync(r => r.Id != round.Id && r.SemesterId == round.SemesterId
            && r.CourseCode == round.CourseCode && r.RoundType == round.RoundType
            && r.OpensAt < round.DeadlineAt && round.OpensAt < r.DeadlineAt, ct);

    public Task<bool> OtherOpenExistsAsync(RegistrationRound round, CancellationToken ct) =>
        db.RegistrationRounds.AnyAsync(r => r.Id != round.Id && r.Status == RoundStatuses.Open
            && r.CourseCode == round.CourseCode && r.RoundType == round.RoundType, ct);

    public void Add(RegistrationRound round) => db.RegistrationRounds.Add(round);

    public void AddEvent<TPayload>(string eventType, Guid roundId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Registration, eventType, "RegistrationRound", roundId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

/// <summary>Giá trị cột <c>status</c> (CHECK <c>ck_registration_rounds_status</c>).</summary>
public static class RoundStatuses
{
    public const string Scheduled = "SCHEDULED";
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
}

/// <summary>Giá trị cột <c>round_type</c>.</summary>
public static class RoundTypes
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string ChangeRequest = "CHANGE_REQUEST";
}

/// <summary>Mã môn (CHECK <c>ck_registration_rounds_course</c>).</summary>
public static class CourseCodes
{
    public const string Exe101 = "EXE101";
    public const string Exe201 = "EXE201";
}
