using System.Security.Claims;
using Academic.Repositories.Lecturers;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Academic.Services.Lecturers;

public interface ILecturerService
{
    Task<PagedResponse<LecturerResponse>> ListAsync(LecturerQuery query, ClaimsPrincipal user, CancellationToken ct);

    Task<LecturerResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Cho Team (<c>GET /internal/lecturers/{id}</c>): đủ trường, kể cả giảng viên đã khóa.</summary>
    Task<LecturerResponse> GetInternalAsync(Guid id, CancellationToken ct);

    Task<LecturerResponse> CreateAsync(LecturerInput input, CancellationToken ct);

    Task<LecturerResponse> UpdateAsync(Guid id, LecturerInput input, CancellationToken ct);

    Task<LecturerResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct);

    Task<LecturerResponse> UpdateMeAsync(LecturerProfileInput input, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Giảng viên (BE-20, W11, L06, S23, S24; rule 9, 11, 19). Admin thêm giảng viên bằng email → <c>LecturerRegistered</c>
/// (Identity cấp quyền đăng nhập, Notification lưu liên lạc, Matching cập nhật read model). Sửa chuyên môn, số nhóm tối đa,
/// trạng thái → <c>LecturerProfileUpdated</c>. Sự kiện qua outbox cùng transaction.
/// </summary>
public sealed class LecturerService(ILecturerRepository lecturers) : ILecturerService
{
    public async Task<PagedResponse<LecturerResponse>> ListAsync(LecturerQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var isAdmin = user.IsInRole(Roles.Admin);
        var (items, total) = await lecturers.ListAsync(query.Search, !isAdmin || query.AcceptingOnly == true, query.Page, query.PageSize, ct);
        return PagedResponse<LecturerResponse>.Create(await ToResponsesAsync(items, isAdmin, ct), query.Page, query.PageSize, total);
    }

    /// <remarks>Người không phải Admin không thấy giảng viên đã khóa (404), giống danh sách.</remarks>
    public async Task<LecturerResponse> GetAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var isAdmin = user.IsInRole(Roles.Admin);
        var lecturer = await lecturers.FindAsync(id, ct);
        if (lecturer is null || (!lecturer.IsActive && !isAdmin))
        {
            throw new NotFoundException("giảng viên", id);
        }
        return await ToResponseAsync(lecturer, isAdmin || user.GetLecturerId() == id, ct);
    }

    public async Task<LecturerResponse> GetInternalAsync(Guid id, CancellationToken ct) =>
        await ToResponseAsync(await FindAsync(id, ct), showPhone: true, ct);

    public async Task<LecturerResponse> CreateAsync(LecturerInput input, CancellationToken ct)
    {
        var email = input.Email!.Trim();
        if (await lecturers.EmailExistsAsync(email, null, ct))
        {
            throw LecturerErrors.EmailTaken(email);
        }
        var domainIds = await ValidateDomainsAsync(input.DomainIds ?? [], [], ct);
        var lecturer = new Lecturer { Id = Guid.NewGuid(), IsActive = input.IsActive ?? true };
        Apply(lecturer, input, email);
        lecturers.Add(lecturer);
        await lecturers.ReplaceDomainsAsync(lecturer.Id, domainIds, ct);
        lecturers.AddEvent(LecturerEventTypes.LecturerRegistered, lecturer.Id, RegisteredEvent(lecturer, domainIds));
        await SaveAsync(email, ct);
        return await ToResponseAsync(lecturer, showPhone: true, ct);
    }

    /// <summary>
    /// Admin sửa mọi trường. Luôn phát <c>LecturerProfileUpdated</c>; đổi họ tên, email hoặc khóa / mở thì phát thêm
    /// <c>LecturerRegistered</c> để Identity và Notification cập nhật tài khoản, liên lạc (xử lý idempotent theo lecturerId).
    /// </summary>
    public async Task<LecturerResponse> UpdateAsync(Guid id, LecturerInput input, CancellationToken ct)
    {
        var lecturer = await FindAsync(id, ct);
        var email = input.Email!.Trim();
        if (await lecturers.EmailExistsAsync(email, id, ct))
        {
            throw LecturerErrors.EmailTaken(email);
        }
        var current = await CurrentDomainIdsAsync(id, ct);
        var domainIds = await ValidateDomainsAsync(input.DomainIds ?? current, current, ct);
        var accountChanged = lecturer.FullName != input.FullName!.Trim()
            || !string.Equals(lecturer.Email, email, StringComparison.OrdinalIgnoreCase)
            || lecturer.IsActive != (input.IsActive ?? lecturer.IsActive);

        Apply(lecturer, input, email);
        lecturer.IsActive = input.IsActive ?? lecturer.IsActive;
        await lecturers.ReplaceDomainsAsync(id, domainIds, ct);
        lecturers.AddEvent(LecturerEventTypes.LecturerProfileUpdated, id, ProfileEvent(lecturer, domainIds));
        if (accountChanged)
        {
            lecturers.AddEvent(LecturerEventTypes.LecturerRegistered, id, RegisteredEvent(lecturer, domainIds));
        }
        await SaveAsync(email, ct);
        return await ToResponseAsync(lecturer, showPhone: true, ct);
    }

    public async Task<LecturerResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct) =>
        await ToResponseAsync(await FindMeAsync(user, ct), showPhone: true, ct);

    /// <summary>Giảng viên chỉ sửa domain chuyên môn (L06); số nhóm tối đa và trạng thái do Admin đặt.</summary>
    public async Task<LecturerResponse> UpdateMeAsync(LecturerProfileInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturer = await FindMeAsync(user, ct);
        var current = await CurrentDomainIdsAsync(lecturer.Id, ct);
        var domainIds = await ValidateDomainsAsync(input.DomainIds!, current, ct);
        await lecturers.ReplaceDomainsAsync(lecturer.Id, domainIds, ct);
        lecturers.AddEvent(LecturerEventTypes.LecturerProfileUpdated, lecturer.Id, ProfileEvent(lecturer, domainIds));
        await lecturers.SaveAsync(ct);
        return await ToResponseAsync(lecturer, showPhone: true, ct);
    }

    private static void Apply(Lecturer lecturer, LecturerInput input, string email)
    {
        lecturer.FullName = input.FullName!.Trim();
        lecturer.Email = email;
        lecturer.Phone = LecturerRules.NormalizePhone(input.Phone);
        lecturer.MaxTeamsExe101 = (short?)input.MaxTeamsExe101;
        lecturer.MaxTeamsExe201 = (short?)input.MaxTeamsExe201;
    }

    /// <summary>
    /// Domain phải có trong danh mục. Domain đã ngừng dùng chỉ được giữ lại nếu giảng viên đang có, không được thêm mới.
    /// Trả danh sách không trùng, sắp tăng dần.
    /// </summary>
    private async Task<List<int>> ValidateDomainsAsync(IReadOnlyCollection<int> requested, IReadOnlyCollection<int> current, CancellationToken ct)
    {
        var ids = requested.Distinct().Order().ToList();
        var found = (await lecturers.FindCatalogDomainsAsync(ids, ct)).ToDictionary(d => d.Id);
        var missing = ids.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw Invalid("domainIds", $"Không tìm thấy domain: {string.Join(", ", missing)}.");
        }
        var inactive = ids.Where(id => !found[id].IsActive && !current.Contains(id)).Select(id => found[id].Code).ToList();
        if (inactive.Count > 0)
        {
            throw Invalid("domainIds", $"Domain đã ngừng dùng: {string.Join(", ", inactive)}.");
        }
        return ids;
    }

    private async Task<List<int>> CurrentDomainIdsAsync(Guid lecturerId, CancellationToken ct) =>
        (await lecturers.ListDomainsAsync([lecturerId], ct)).Select(d => d.Id).ToList();

    private async Task<Lecturer> FindAsync(Guid id, CancellationToken ct) =>
        await lecturers.FindAsync(id, ct) ?? throw new NotFoundException("giảng viên", id);

    /// <summary>Theo claim <c>lecturer_id</c>; token cũ chưa có claim thì theo email.</summary>
    private async Task<Lecturer> FindMeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturer = user.GetLecturerId() is { } id
            ? await lecturers.FindAsync(id, ct)
            : user.GetEmail() is { } email ? await lecturers.FindByEmailAsync(email, ct) : null;
        return lecturer ?? throw new NotFoundException("hồ sơ giảng viên của tài khoản này");
    }

    /// <summary>Hai Admin thêm cùng email một lúc thì DB chặn bằng <c>uq_lecturers_email</c>.</summary>
    private async Task SaveAsync(string email, CancellationToken ct)
    {
        try
        {
            await lecturers.SaveAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw LecturerErrors.EmailTaken(email);
        }
    }

    private async Task<LecturerResponse> ToResponseAsync(Lecturer lecturer, bool showPhone, CancellationToken ct) =>
        (await ToResponsesAsync([lecturer], showPhone, ct)).Single();

    private async Task<IReadOnlyList<LecturerResponse>> ToResponsesAsync(IReadOnlyList<Lecturer> items, bool showPhone, CancellationToken ct)
    {
        var domains = (await lecturers.ListDomainsAsync(items.Select(l => l.Id).ToList(), ct)).ToLookup(d => d.LecturerId);
        return items.Select(l => new LecturerResponse(l.Id, l.FullName, l.Email, showPhone ? l.Phone : null,
            domains[l.Id].Select(d => new CatalogRef(d.Id, d.Code, d.Name)).ToList(), l.MaxTeamsExe101, l.MaxTeamsExe201, l.IsActive)).ToList();
    }

    private static LecturerRegisteredEvent RegisteredEvent(Lecturer l, IReadOnlyList<int> domainIds) =>
        new(l.Id, l.FullName, l.Email, domainIds, l.MaxTeamsExe101, l.MaxTeamsExe201, l.IsActive);

    private static LecturerProfileUpdatedEvent ProfileEvent(Lecturer l, IReadOnlyList<int> domainIds) =>
        new(l.Id, domainIds, l.MaxTeamsExe101, l.MaxTeamsExe201, l.IsActive);

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Lỗi của module Lecturers.</summary>
public static class LecturerErrors
{
    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException EmailTaken(string email) =>
        new("lecturer-email-taken", "Email đã có giảng viên khác", $"Email {email} đã thuộc một giảng viên khác.");
}

public static class LecturersModule
{
    /// <summary>Module Lecturers (BE-20): giảng viên – W11, L06, S23, S24.</summary>
    public static IServiceCollection AddLecturersModule(this IServiceCollection services)
    {
        services.AddScoped<ILecturerRepository, LecturerRepository>();
        services.AddScoped<ILecturerService, LecturerService>();
        return services;
    }
}
