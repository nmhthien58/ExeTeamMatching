using System.Security.Claims;
using Academic.Repositories.Lecturers;
using Academic.Repositories.Mentors;
using Academic.Repositories.Semesters;
using Academic.Services.Lecturers;
using Academic.Services.Teams;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Academic.Services.Mentors;

public interface IMentorService
{
    Task<PagedResponse<MentorResponse>> ListAsync(MentorQuery query, CancellationToken ct);

    /// <summary>Chỉ Admin và chính mentor đó; người khác 403 (hồ sơ có email, số nhóm).</summary>
    Task<MentorResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Cho Team (<c>GET /internal/mentors/{id}</c>): đủ trường, kể cả mentor đã khóa.</summary>
    Task<MentorResponse> GetInternalAsync(Guid id, CancellationToken ct);

    Task<MentorResponse> CreateAsync(MentorInput input, ClaimsPrincipal user, CancellationToken ct);

    Task<MentorResponse> UpdateAsync(Guid id, MentorInput input, CancellationToken ct);

    Task<MentorResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct);

    Task<MentorResponse> UpdateMeAsync(MentorProfileInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// Áp thay đổi của giảng viên lên hồ sơ mentor gắn với họ; chỉ ghi vào phiên làm việc, lưu cùng transaction outbox của giảng viên.
    /// <paramref name="newEmail"/> khác null: giảng viên tạo mới hoặc đổi email, trùng mentor khác → 409, mentor gắn với họ đổi theo.
    /// <paramref name="locked"/>: giảng viên vừa bị khóa, mentor gắn với họ khóa theo. Mở khóa giảng viên không đi qua đây, Bộ môn tự mở mentor.
    /// </summary>
    Task StageLecturerChangeAsync(Guid lecturerId, string? newEmail, bool locked, CancellationToken ct);
}

/// <summary>
/// Mentor (BE-66, W27, M01; rule 19, 20). Admin tạo mentor → <c>MentorRegistered</c> (Identity tạo tài khoản, Notification mời đăng nhập,
/// Matching cập nhật read model). Mọi lần sửa đều phát lại sự kiện này, các bên xử lý idempotent theo mentorId.
/// Số nhóm đang mentor thuộc Team Service (<c>POST /internal/mentors/team-counts</c>, học kỳ hiện hành, nhóm chưa giải tán):
/// điền <c>currentTeamCount</c>, lọc <c>hasCapacity</c> trên toàn bộ dữ liệu trước khi phân trang và chặn <c>max-below-current</c>.
/// Team lỗi: lúc đọc thì <c>currentTeamCount</c> = 0 và bỏ qua <c>hasCapacity</c> (như trước khi có API đếm), còn sửa tối đa thì 503.
/// Khóa mentor phát sự kiện với <c>isActive = false</c>; Team gỡ mentor khỏi nhóm.
/// Email do Bộ môn cấp, không trùng mentor, giảng viên, sinh viên; giảng viên kiêm mentor dùng luôn email giảng viên.
/// </summary>
public sealed class MentorService(
    IMentorRepository mentors,
    ILecturerRepository lecturers,
    ISemesterRepository semesters,
    ITeamClient team,
    ILogger<MentorService> logger) : IMentorService
{
    public async Task<PagedResponse<MentorResponse>> ListAsync(MentorQuery query, CancellationToken ct)
    {
        var filter = new MentorFilter(query.Search, query.DomainId, query.IsInternal, query.Page, query.PageSize);
        if (query.HasCapacity is { } hasCapacity && await ListWithCapacityAsync(filter, hasCapacity, ct) is { } filtered)
        {
            return filtered;
        }
        var (items, total) = await mentors.ListAsync(filter, ct);
        return PagedResponse<MentorResponse>.Create(await ToResponsesAsync(items, ct), query.Page, query.PageSize, total);
    }

    /// <summary>
    /// Lọc <c>hasCapacity</c> (đang mentor &lt; tối đa) trên mọi mentor khớp bộ lọc rồi mới phân trang.
    /// Team lỗi → null: người gọi trả danh sách không lọc theo số nhóm.
    /// </summary>
    private async Task<PagedResponse<MentorResponse>?> ListWithCapacityAsync(MentorFilter filter, bool hasCapacity, CancellationToken ct)
    {
        var rows = await mentors.ListCapacityRowsAsync(filter, ct);
        if (await TryCountsAsync(rows.Select(r => r.Id).ToList(), ct) is not { } counts)
        {
            return null;
        }
        var matching = rows.Where(r => (counts.GetValueOrDefault(r.Id) < r.MaxTeams) == hasCapacity).ToList();
        var pageIds = matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(r => r.Id).ToList();
        var items = await mentors.ListByIdsAsync(pageIds, ct);
        return PagedResponse<MentorResponse>.Create(await ToResponsesAsync(items, counts, ct), filter.Page, filter.PageSize, matching.Count);
    }

    public async Task<MentorResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.Admin) && user.GetMentorId() != id)
        {
            throw new ForbiddenException();
        }
        return await ToResponseAsync(await FindAsync(id, ct), ct);
    }

    public async Task<MentorResponse> GetInternalAsync(Guid id, CancellationToken ct) => await ToResponseAsync(await FindAsync(id, ct), ct);

    public async Task<MentorResponse> CreateAsync(MentorInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var isInternal = input.IsInternal ?? false;
        var lecturer = await ResolveLecturerAsync(input.LecturerId, null, isInternal, null, ct);
        var email = lecturer?.Email ?? MentorRules.RequireEmail(input.Email);
        await RequireFreeEmailAsync(email, lecturer, null, ct);
        var domainIds = await LecturerService.ValidateDomainsAsync(lecturers, input.DomainIds ?? [], [], ct);

        var mentor = new Mentor { Id = Guid.NewGuid(), IsActive = input.IsActive ?? true, CreatedBy = user.GetUserId() };
        Apply(mentor, input, email, isInternal, lecturer?.Id);
        mentors.Add(mentor);
        await mentors.ReplaceDomainsAsync(mentor.Id, domainIds, ct);
        mentors.AddEvent(MentorEventTypes.MentorRegistered, mentor.Id, RegisteredEvent(mentor, domainIds));
        await SaveAsync(email, ct);
        return await ToResponseAsync(mentor, ct);
    }

    /// <summary>
    /// Admin sửa hồ sơ, đặt số nhóm tối đa, khóa / mở. Luôn phát <c>MentorRegistered</c> để các bên cập nhật.
    /// Mở khóa mentor khi giảng viên liên kết đang khóa → 409 <c>lecturer-locked</c>.
    /// </summary>
    public async Task<MentorResponse> UpdateAsync(Guid id, MentorInput input, CancellationToken ct)
    {
        var mentor = await FindAsync(id, ct);
        var isInternal = input.IsInternal ?? mentor.IsInternal;
        var lecturer = await ResolveLecturerAsync(input.LecturerId, mentor.LecturerId, isInternal, id, ct);
        // Khóa giảng viên thì mentor gắn với họ khóa theo (StageLecturerChangeAsync): không mở lại mentor khi giảng viên vẫn khóa.
        // Sửa khác (kể cả giữ nguyên trạng thái khóa) vẫn được.
        if (!mentor.IsActive && input.IsActive == true && lecturer is { IsActive: false })
        {
            throw MentorErrors.ReopenWhileLecturerLocked();
        }
        var email = lecturer?.Email ?? MentorRules.RequireEmail(input.Email);
        await RequireFreeEmailAsync(email, lecturer, id, ct);
        var current = await CurrentDomainIdsAsync(id, ct);
        var domainIds = await LecturerService.ValidateDomainsAsync(lecturers, input.DomainIds ?? current, current, ct);

        // Chỉ kiểm tra khi đổi số tối đa: sửa khác không bị chặn nếu mentor lỡ vượt tối đa do Bộ môn gán tay.
        IReadOnlyDictionary<Guid, int>? counts = null;
        if (input.MaxTeams != mentor.MaxTeams)
        {
            counts = await RequireCountsAsync(new[] { id }, ct);
            var currentTeams = counts.GetValueOrDefault(id);
            if (input.MaxTeams < currentTeams)
            {
                throw MentorErrors.MaxBelowCurrent(currentTeams);
            }
        }

        Apply(mentor, input, email, isInternal, lecturer?.Id);
        mentor.IsActive = input.IsActive ?? mentor.IsActive;
        mentor.PreferredTeams = MentorRules.CapPreferred(mentor.PreferredTeams, mentor.MaxTeams);
        await mentors.ReplaceDomainsAsync(id, domainIds, ct);
        mentors.AddEvent(MentorEventTypes.MentorRegistered, id, RegisteredEvent(mentor, domainIds));
        await SaveAsync(email, ct);
        return counts is null ? await ToResponseAsync(mentor, ct) : (await ToResponsesAsync([mentor], counts, ct)).Single();
    }

    public async Task<MentorResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct) =>
        await ToResponseAsync(await FindMeAsync(user, ct), ct);

    /// <summary>Mentor sửa số nhóm mong muốn (không vượt tối đa do Bộ môn đặt), giới thiệu, domain (M01).</summary>
    public async Task<MentorResponse> UpdateMeAsync(MentorProfileInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var mentor = await FindMeAsync(user, ct);
        if (input.PreferredTeams > mentor.MaxTeams)
        {
            throw MentorErrors.PreferredAboveMax(mentor.MaxTeams);
        }
        var current = await CurrentDomainIdsAsync(mentor.Id, ct);
        var domainIds = await LecturerService.ValidateDomainsAsync(lecturers, input.DomainIds ?? current, current, ct);

        mentor.PreferredTeams = (short?)input.PreferredTeams ?? mentor.PreferredTeams;
        if (input.Description is not null)
        {
            mentor.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        }
        await mentors.ReplaceDomainsAsync(mentor.Id, domainIds, ct);
        mentors.AddEvent(MentorEventTypes.MentorRegistered, mentor.Id, RegisteredEvent(mentor, domainIds));
        await mentors.SaveAsync(ct);
        return await ToResponseAsync(mentor, ct);
    }

    public async Task StageLecturerChangeAsync(Guid lecturerId, string? newEmail, bool locked, CancellationToken ct)
    {
        var mentor = await mentors.FindByLecturerAsync(lecturerId, ct);
        if (newEmail is not null && await mentors.EmailExistsAsync(newEmail, mentor?.Id, ct))
        {
            throw MentorErrors.EmailExists(newEmail);
        }
        if (mentor is null || (newEmail is null && !mentor.IsActive))
        {
            return;
        }
        mentor.Email = newEmail ?? mentor.Email;
        mentor.IsActive = mentor.IsActive && !locked;
        mentors.AddEvent(MentorEventTypes.MentorRegistered, mentor.Id, RegisteredEvent(mentor, await CurrentDomainIdsAsync(mentor.Id, ct)));
    }

    private static void Apply(Mentor mentor, MentorInput input, string email, bool isInternal, Guid? lecturerId)
    {
        mentor.FullName = input.FullName!.Trim();
        mentor.Email = email;
        mentor.Organization = string.IsNullOrWhiteSpace(input.Organization) ? null : input.Organization.Trim();
        mentor.IsInternal = isInternal;
        mentor.LecturerId = lecturerId;
        mentor.MaxTeams = (short)input.MaxTeams!.Value;
    }

    /// <summary>
    /// Giảng viên mentor gắn với sau lần lưu này (rule 20): <paramref name="requested"/> bỏ trống thì giữ liên kết <paramref name="current"/>,
    /// mentor ngoài trường không có liên kết. Chỉ mentor trong trường mới gắn được; giảng viên phải tồn tại.
    /// Liên kết mới phải là giảng viên chưa khóa và chưa có mentor; liên kết cũ vẫn giữ được khi giảng viên bị khóa sau đó.
    /// </summary>
    private async Task<Lecturer?> ResolveLecturerAsync(Guid? requested, Guid? current, bool isInternal, Guid? mentorId, CancellationToken ct)
    {
        if (requested is not null && !isInternal)
        {
            throw Invalid("lecturerId", "Chỉ mentor trong trường mới gắn được với giảng viên.");
        }
        if ((isInternal ? requested ?? current : null) is not { } id)
        {
            return null;
        }
        var lecturer = await lecturers.FindAsync(id, ct) ?? throw Invalid("lecturerId", "Không tìm thấy giảng viên.");
        if (id == current)
        {
            return lecturer;
        }
        if (!lecturer.IsActive)
        {
            throw MentorErrors.LecturerLocked();
        }
        if (await mentors.LecturerLinkedAsync(id, mentorId, ct))
        {
            throw MentorErrors.LecturerHasMentor();
        }
        return lecturer;
    }

    /// <summary>Email chưa có mentor khác; mentor không gắn giảng viên thì cũng không được trùng giảng viên, sinh viên.</summary>
    private async Task RequireFreeEmailAsync(string email, Lecturer? lecturer, Guid? mentorId, CancellationToken ct)
    {
        if (await mentors.EmailExistsAsync(email, mentorId, ct) || (lecturer is null && await mentors.EmailUsedByPersonAsync(email, ct)))
        {
            throw MentorErrors.EmailExists(email);
        }
    }

    private async Task<List<int>> CurrentDomainIdsAsync(Guid mentorId, CancellationToken ct) =>
        (await mentors.ListDomainsAsync([mentorId], ct)).Select(d => d.Id).ToList();

    private async Task<Mentor> FindAsync(Guid id, CancellationToken ct) =>
        await mentors.FindAsync(id, ct) ?? throw new NotFoundException("mentor", id);

    /// <summary>Theo claim <c>mentor_id</c>; token chưa có claim thì theo email.</summary>
    private async Task<Mentor> FindMeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var mentor = user.GetMentorId() is { } id
            ? await mentors.FindAsync(id, ct)
            : user.GetEmail() is { } email ? await mentors.FindByEmailAsync(email, ct) : null;
        return mentor ?? throw new NotFoundException("hồ sơ mentor của tài khoản này");
    }

    /// <summary>Hai Admin thao tác cùng lúc thì DB chặn bằng <c>uq_mentors_email</c>, <c>uq_mentors_lecturer_id</c>.</summary>
    private async Task SaveAsync(string email, CancellationToken ct)
    {
        try
        {
            await mentors.SaveAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique)
        {
            throw unique.ConstraintName == "uq_mentors_lecturer_id" ? MentorErrors.LecturerHasMentor() : MentorErrors.EmailExists(email);
        }
    }

    private async Task<MentorResponse> ToResponseAsync(Mentor mentor, CancellationToken ct) =>
        (await ToResponsesAsync([mentor], ct)).Single();

    /// <summary>Kèm <c>currentTeamCount</c> từ Team; Team lỗi thì 0 (màn hình vẫn hiện danh sách).</summary>
    private async Task<IReadOnlyList<MentorResponse>> ToResponsesAsync(IReadOnlyList<Mentor> items, CancellationToken ct) =>
        await ToResponsesAsync(items, await TryCountsAsync(items.Select(m => m.Id).ToList(), ct) ?? new Dictionary<Guid, int>(), ct);

    private async Task<IReadOnlyList<MentorResponse>> ToResponsesAsync(IReadOnlyList<Mentor> items, IReadOnlyDictionary<Guid, int> counts, CancellationToken ct)
    {
        var domains = (await mentors.ListDomainsAsync(items.Select(m => m.Id).ToList(), ct)).ToLookup(d => d.MentorId);
        return items.Select(m => new MentorResponse(m.Id, m.FullName, m.Email, m.Organization, m.IsInternal, m.LecturerId, m.Description,
            domains[m.Id].Select(d => new CatalogRef(d.Id, d.Code, d.Name)).ToList(), m.MaxTeams, m.PreferredTeams,
            counts.GetValueOrDefault(m.Id), m.IsActive)).ToList();
    }

    /// <summary>
    /// Số nhóm đang mentor trong học kỳ hiện hành. Chưa có học kỳ hiện hành → mọi mentor 0 nhóm (không gọi Team).
    /// Team lỗi → <see cref="TeamUnavailableException"/>.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, int>> FetchCountsAsync(IReadOnlyCollection<Guid> mentorIds, CancellationToken ct)
    {
        if (mentorIds.Count == 0 || await semesters.GetCurrentAsync(ct) is not { } semester)
        {
            return new Dictionary<Guid, int>();
        }
        return await team.GetMentorTeamCountsAsync(mentorIds, semester.Id, ct);
    }

    /// <summary>Như <see cref="FetchCountsAsync"/> nhưng Team lỗi thì ghi log và trả null, để luồng đọc không hỏng.</summary>
    private async Task<IReadOnlyDictionary<Guid, int>?> TryCountsAsync(IReadOnlyCollection<Guid> mentorIds, CancellationToken ct)
    {
        try
        {
            return await FetchCountsAsync(mentorIds, ct);
        }
        catch (TeamUnavailableException ex)
        {
            logger.LogWarning(ex, "Không lấy được số nhóm của {Count} mentor từ Team; currentTeamCount = 0, bỏ qua hasCapacity.", mentorIds.Count);
            return null;
        }
    }

    /// <summary>Như <see cref="FetchCountsAsync"/> cho luồng ghi: Team lỗi → 503 <c>team-unavailable</c>, không bỏ qua kiểm tra.</summary>
    private async Task<IReadOnlyDictionary<Guid, int>> RequireCountsAsync(IReadOnlyCollection<Guid> mentorIds, CancellationToken ct)
    {
        try
        {
            return await FetchCountsAsync(mentorIds, ct);
        }
        catch (TeamUnavailableException)
        {
            throw MentorErrors.TeamUnavailable();
        }
    }

    private static MentorRegisteredEvent RegisteredEvent(Mentor m, IReadOnlyList<int> domainIds) =>
        new(m.Id, m.FullName, m.Email, m.Organization, m.IsInternal, m.LecturerId, domainIds, m.MaxTeams, m.IsActive);

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Lỗi của module Mentors.</summary>
public static class MentorErrors
{
    public static ConflictException EmailExists(string email) =>
        new("email-exists", "Email đã được dùng", $"Email {email} đã thuộc một mentor, giảng viên hoặc sinh viên khác.");

    public static HttpStatusException PreferredAboveMax(int maxTeams) =>
        new(400, "preferred-above-max", "Số nhóm mong muốn vượt tối đa", $"Số nhóm mong muốn không được vượt {maxTeams}, mức tối đa do Bộ môn đặt.");

    public static ConflictException LecturerLocked() =>
        new("lecturer-locked", "Giảng viên đang bị khóa", "Mở khóa giảng viên rồi mới gắn được với hồ sơ mentor.");

    /// <summary>Cùng mã <c>lecturer-locked</c>: mở khóa mentor trong khi giảng viên liên kết đang khóa.</summary>
    public static ConflictException ReopenWhileLecturerLocked() =>
        new("lecturer-locked", "Giảng viên đang bị khóa", "Giảng viên gắn với hồ sơ mentor này đang bị khóa. Mở khóa giảng viên rồi mới mở khóa mentor.");

    public static ConflictException MaxBelowCurrent(int currentTeams) =>
        new("max-below-current", "Số nhóm tối đa nhỏ hơn số nhóm đang mentor",
            $"Mentor đang phụ trách {currentTeams} nhóm trong học kỳ hiện hành; số nhóm tối đa không được nhỏ hơn số này.");

    /// <summary>Không kiểm tra được số nhóm đang mentor (Team không trả lời) nên không lưu số tối đa mới.</summary>
    public static HttpStatusException TeamUnavailable() =>
        new(503, "team-unavailable", "Chưa kiểm tra được số nhóm đang mentor",
            "Team Service đang không trả lời nên chưa đổi được số nhóm tối đa. Thử lại sau.");

    public static ConflictException LecturerHasMentor() =>
        new("lecturer-has-mentor", "Giảng viên đã có hồ sơ mentor", "Mỗi giảng viên chỉ gắn với một hồ sơ mentor.");
}

public static class MentorsModule
{
    /// <summary>Module Mentors (BE-66): mentor – W27, M01.</summary>
    public static IServiceCollection AddMentorsModule(this IServiceCollection services)
    {
        services.AddScoped<IMentorRepository, MentorRepository>();
        services.AddScoped<IMentorService, MentorService>();
        return services;
    }
}
