using System.Text.RegularExpressions;
using FluentValidation;

namespace Academic.Services.Lecturers;

// Schema của academic.yaml (tag Lecturers). JSON camelCase.

/// <summary>Schema <c>CatalogRef</c>.</summary>
public sealed record CatalogRef(int Id, string Code, string Name);

/// <summary>Schema <c>Lecturer</c>. <see cref="Phone"/> chỉ trả cho Admin, chính giảng viên đó và Team (rule 11); người khác nhận null.</summary>
public sealed record LecturerResponse(
    Guid Id, string FullName, string Email, string? Phone, IReadOnlyList<CatalogRef> Domains,
    int? MaxTeamsExe101, int? MaxTeamsExe201, bool IsActive);

/// <summary>Schema <c>LecturerInput</c>. <see cref="IsActive"/> bỏ trống: thêm mới thì true, sửa thì giữ nguyên.</summary>
public sealed record LecturerInput(
    string? FullName, string? Email, string? Phone, IReadOnlyList<int>? DomainIds,
    int? MaxTeamsExe101, int? MaxTeamsExe201, bool? IsActive);

/// <summary>Body của <c>PUT /api/lecturers/me</c>: giảng viên chỉ sửa domain chuyên môn (số nhóm tối đa do Admin đặt).</summary>
public sealed record LecturerProfileInput(IReadOnlyList<int>? DomainIds);

/// <summary>Query của <c>GET /api/lecturers</c>. Người không phải Admin chỉ thấy giảng viên đang hoạt động.</summary>
public sealed record LecturerQuery(string? Search, int Page = 1, int PageSize = 20);

public sealed partial class LecturerInputValidator : AbstractValidator<LecturerInput>
{
    public LecturerInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nhập họ tên.").MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Nhập email.")
            .Must(e => e!.Trim().Length <= 254 && EmailPattern().IsMatch(e.Trim())).When(x => !string.IsNullOrWhiteSpace(x.Email), ApplyConditionTo.CurrentValidator).WithMessage("Email không đúng định dạng.");
        RuleFor(x => x.Phone).Must(p => PhonePattern().IsMatch(LecturerRules.NormalizePhone(p)!)).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("SĐT phải gồm 9–14 chữ số, có thể bắt đầu bằng +.");
        RuleFor(x => x.MaxTeamsExe101).InclusiveBetween(0, 200).WithMessage("Số nhóm tối đa EXE101 từ 0 đến 200, để trống là không giới hạn.");
        RuleFor(x => x.MaxTeamsExe201).InclusiveBetween(0, 200).WithMessage("Số nhóm tối đa EXE201 từ 0 đến 200, để trống là không giới hạn.");
        RuleFor(x => x.DomainIds).Must(ids => ids!.Count <= LecturerRules.MaxDomains).When(x => x.DomainIds is not null)
            .WithMessage($"Tối đa {LecturerRules.MaxDomains} domain.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9]{9,14}$")]
    private static partial Regex PhonePattern();
}

public sealed class LecturerProfileInputValidator : AbstractValidator<LecturerProfileInput>
{
    public LecturerProfileInputValidator()
    {
        RuleFor(x => x.DomainIds).NotNull().WithMessage("Thiếu domainIds.")
            .Must(ids => ids!.Count <= LecturerRules.MaxDomains).When(x => x.DomainIds is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage($"Tối đa {LecturerRules.MaxDomains} domain.");
    }
}

public sealed class LecturerQueryValidator : AbstractValidator<LecturerQuery>
{
    public LecturerQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page bắt đầu từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize từ 1 đến 100.");
    }
}

public static partial class LecturerRules
{
    /// <summary>Danh mục có 18 domain D01–D18 (contract: <c>maxItems: 18</c>).</summary>
    public const int MaxDomains = 18;

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch, ngoặc; chuỗi rỗng thành null. Lưu dạng chuỗi (rule 11).</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }
        return PhoneSeparators().Replace(phone, string.Empty);
    }

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();
}

// Payload sự kiện exe.student (docs/contracts/events.md), key = lecturerId.

public static class LecturerEventTypes
{
    public const string LecturerRegistered = "LecturerRegistered";
    public const string LecturerProfileUpdated = "LecturerProfileUpdated";
}

public sealed record LecturerRegisteredEvent(
    Guid LecturerId, string FullName, string Email, IReadOnlyList<int> DomainIds, int? MaxTeamsExe101, int? MaxTeamsExe201, bool IsActive);

public sealed record LecturerProfileUpdatedEvent(Guid LecturerId, IReadOnlyList<int> DomainIds, int? MaxTeamsExe101, int? MaxTeamsExe201, bool IsActive);
