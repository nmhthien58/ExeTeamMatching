using System.Security.Claims;
using Academic.Repositories.Rounds;
using Academic.Repositories.Semesters;
using Academic.Services.Caching;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academic.Services.Rounds;

public interface IRoundService
{
    Task<IReadOnlyList<RoundResponse>> ListAsync(RoundQuery query, CancellationToken ct);

    /// <summary>Đợt đang mở, lọc theo môn nếu có (banner hạn chót S01, L01). Cache Redis.</summary>
    Task<IReadOnlyList<RoundResponse>> ListOpenAsync(string? courseCode, CancellationToken ct);

    /// <summary>Đợt đang mở và chưa quá hạn của môn + loại đợt (Team hỏi trước khi ghi, BE-22). Cache Redis; 404 khi không có.</summary>
    Task<RoundResponse> GetOpenAsync(string courseCode, string roundType, CancellationToken ct);

    Task<RoundResponse> CreateAsync(RoundInput input, ClaimsPrincipal user, CancellationToken ct);

    Task<RoundResponse> UpdateAsync(Guid id, RoundInput input, CancellationToken ct);

    Task<RoundResponse> OpenAsync(Guid id, CancellationToken ct);

    Task<RoundResponse> CloseAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Đợt đăng ký (BE-16, W04, rule 6): SCHEDULED → OPEN → CLOSED, chỉ đi một chiều.
/// Mở phát <c>RoundOpened</c> (Job hẹn tự đóng đúng hạn chót), khóa phát <c>RoundClosed</c>, qua outbox cùng transaction.
/// </summary>
public sealed class RoundService(IRoundRepository rounds, ISemesterRepository semesters, AcademicCache cache, TimeProvider clock) : IRoundService
{
    public async Task<IReadOnlyList<RoundResponse>> ListAsync(RoundQuery query, CancellationToken ct) =>
        (await rounds.ListAsync(query.SemesterId, query.CourseCode, query.Status, ct)).Select(RoundResponse.From).ToList();

    public async Task<IReadOnlyList<RoundResponse>> ListOpenAsync(string? courseCode, CancellationToken ct)
    {
        var open = await cache.GetOrLoadAsync(AcademicCacheKeys.OpenRounds, async token =>
            (await rounds.ListAsync(null, null, RoundStatuses.Open, token)).Select(RoundResponse.From).ToList(), ct);
        return courseCode is null ? open : open.Where(r => r.CourseCode == courseCode).ToList();
    }

    /// <remarks>
    /// Đợt đã quá hạn chót nhưng Job chưa kịp khóa vẫn coi là đã đóng: rule 6 khóa đúng hạn chót,
    /// không phụ thuộc lúc RoundAutoCloseJob chạy.
    /// </remarks>
    public async Task<RoundResponse> GetOpenAsync(string courseCode, string roundType, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return (await ListOpenAsync(courseCode, ct)).FirstOrDefault(r => r.RoundType == roundType && r.DeadlineAt > now)
            ?? throw new NotFoundException($"đợt {roundType} đang mở của {courseCode}");
    }

    public async Task<RoundResponse> CreateAsync(RoundInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var round = new RegistrationRound { Status = RoundStatuses.Scheduled, CreatedBy = user.GetUserId() };
        await ApplyAsync(round, input, ct);
        rounds.Add(round);
        await SaveAsync(ct);
        return RoundResponse.From(round);
    }

    /// <summary>
    /// Đợt chưa mở sửa được mọi trường. Đợt đang mở chỉ đổi tên: Job đã hẹn giờ đóng theo hạn chót cũ khi nhận RoundOpened.
    /// Đợt đã khóa không sửa.
    /// </summary>
    public async Task<RoundResponse> UpdateAsync(Guid id, RoundInput input, CancellationToken ct)
    {
        var round = await FindAsync(id, ct);
        switch (round.Status)
        {
            case RoundStatuses.Closed:
                throw RoundErrors.RoundClosed();
            case RoundStatuses.Open:
                if (input.SemesterId != round.SemesterId || input.CourseCode != round.CourseCode || input.RoundType != round.RoundType
                    || ToDbTime(input.OpensAt!.Value) != round.OpensAt || ToDbTime(input.DeadlineAt!.Value) != round.DeadlineAt)
                {
                    throw RoundErrors.OpenRoundLocked();
                }
                round.Name = input.Name!.Trim();
                break;
            default:
                await ApplyAsync(round, input, ct);
                break;
        }
        await SaveAsync(ct);
        return RoundResponse.From(round);
    }

    /// <summary>Mở thủ công đợt SCHEDULED. Mở lại đợt đang mở thì trả nguyên trạng.</summary>
    public async Task<RoundResponse> OpenAsync(Guid id, CancellationToken ct)
    {
        var round = await FindAsync(id, ct);
        if (round.Status == RoundStatuses.Open)
        {
            return RoundResponse.From(round);
        }
        if (round.Status == RoundStatuses.Closed)
        {
            throw RoundErrors.RoundClosed();
        }
        if (round.DeadlineAt <= clock.GetUtcNow())
        {
            throw RoundErrors.Invalid("deadlineAt", "Hạn chót đã qua, sửa hạn chót trước khi mở đợt.");
        }
        if (await rounds.OtherOpenExistsAsync(round, ct))
        {
            throw RoundErrors.AlreadyOpen(round.CourseCode, round.RoundType);
        }

        round.Status = RoundStatuses.Open;
        rounds.AddEvent(RoundEventTypes.RoundOpened, round.Id, RoundEvent.From(round));
        await SaveAsync(ct);
        return RoundResponse.From(round);
    }

    /// <summary>Khóa đợt đang mở (Admin, hoặc Job khi tới hạn). Khóa lại đợt đã khóa thì trả nguyên trạng, để Job thử lại an toàn.</summary>
    public async Task<RoundResponse> CloseAsync(Guid id, CancellationToken ct)
    {
        var round = await FindAsync(id, ct);
        if (round.Status == RoundStatuses.Closed)
        {
            return RoundResponse.From(round);
        }
        if (round.Status == RoundStatuses.Scheduled)
        {
            throw RoundErrors.RoundClosed();
        }

        round.Status = RoundStatuses.Closed;
        round.ClosedAt = clock.GetUtcNow();
        rounds.AddEvent(RoundEventTypes.RoundClosed, round.Id, RoundEvent.From(round));
        await SaveAsync(ct);
        return RoundResponse.From(round);
    }

    /// <summary>UTC, làm tròn xuống micro giây như <c>timestamptz</c>, để giá trị trong bộ nhớ khớp giá trị đọc lại từ DB.</summary>
    private static DateTimeOffset ToDbTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private async Task<RegistrationRound> FindAsync(Guid id, CancellationToken ct) =>
        await rounds.FindAsync(id, ct) ?? throw new NotFoundException("đợt đăng ký", id);

    /// <summary>Gán dữ liệu đã qua <see cref="RoundInputValidator"/>, kiểm tra học kỳ và chồng thời gian.</summary>
    private async Task ApplyAsync(RegistrationRound round, RoundInput input, CancellationToken ct)
    {
        if (!await semesters.ExistsAsync(input.SemesterId!.Value, ct))
        {
            throw RoundErrors.Invalid("semesterId", "Không tìm thấy học kỳ.");
        }
        round.SemesterId = input.SemesterId.Value;
        round.CourseCode = input.CourseCode!;
        round.RoundType = input.RoundType!;
        round.Name = input.Name!.Trim();
        round.OpensAt = ToDbTime(input.OpensAt!.Value);
        round.DeadlineAt = ToDbTime(input.DeadlineAt!.Value);
        if (await rounds.OverlapsAsync(round, ct))
        {
            throw RoundErrors.Overlap();
        }
    }

    /// <summary>Lưu rồi xóa cache đợt đang mở. Hai Admin tạo đợt chồng nhau cùng lúc thì DB chặn bằng EXCLUDE.</summary>
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await rounds.SaveAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            throw RoundErrors.Overlap();
        }
        await cache.InvalidateAsync(AcademicCacheKeys.OpenRounds, ct);
    }
}

/// <summary>Lỗi của module Rounds (mã ở docs/contracts/README.md).</summary>
public static class RoundErrors
{
    public static ConflictException RoundClosed() =>
        new("round-closed", "Đợt đăng ký chưa mở hoặc đã khóa", "Đợt đã khóa không sửa hay mở lại được; chỉ khóa được đợt đang mở.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException Overlap() =>
        new("round-overlap", "Trùng thời gian đợt khác", "Học kỳ đã có đợt cùng môn và cùng loại trong khoảng thời gian này.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException AlreadyOpen(string courseCode, string roundType) =>
        new("round-already-open", "Đã có đợt đang mở", $"Môn {courseCode} đang có một đợt {roundType} mở; khóa đợt đó trước.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException OpenRoundLocked() =>
        new("round-open-locked", "Đợt đang mở chỉ đổi được tên",
            "Đợt đã mở thì không đổi học kỳ, môn, loại, giờ mở hay hạn chót (Job đã hẹn giờ khóa). Khóa đợt rồi tạo đợt mới nếu cần.");

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
