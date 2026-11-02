using System.Text.RegularExpressions;
using Academic.Services.Lecturers;
using FluentValidation;

namespace Academic.Services.Mentors;

// Schema của academic.yaml (tag Mentors). JSON camelCase.

/// <summary>
/// Schema <c>Mentor</c>. <see cref="CurrentTeamCount"/> thuộc Team Service – tạm thời 0 cho tới khi Team có API đếm số nhóm của mentor.
/// </summary>
public sealed record MentorResponse(
    Guid Id, string FullName, string Email, string? Organization, bool IsInternal, Guid? LecturerId, string? Description,
    IReadOnlyList<CatalogRef> Domains, int MaxTeams, int? PreferredTeams, int CurrentTeamCount, bool IsActive);

/// <summary>
/// Schema <c>MentorInput</c>. Khi sửa, <see cref="IsInternal"/>, <see cref="IsActive"/>, <see cref="DomainIds"/> bỏ trống thì giữ nguyên;
/// <see cref="LecturerId"/> bỏ trống cũng giữ liên kết cũ, muốn gỡ thì đặt <see cref="IsInternal"/> = false.
/// </summary>
public sealed record MentorInput(
    string? FullName, string? Email, string? Organization, bool? IsInternal, Guid? LecturerId, IReadOnlyList<int>? DomainIds,
    int? MaxTeams, bool? IsActive);

/// <summary>
/// Body của <c>PUT /api/mentors/me</c>. Trường bỏ trống thì giữ nguyên; <see cref="Description"/> là chuỗi rỗng thì xóa giới thiệu.
/// </summary>
public sealed record MentorProfileInput(int? PreferredTeams, string? Description, IReadOnlyList<int>? DomainIds);

/// <summary>
/// Query của <c>GET /api/mentors</c>. <see cref="HasCapacity"/> nhận theo contract nhưng chưa có tác dụng
/// cho tới khi Team có API đếm số nhóm của mentor.
/// </summary>
public sealed record MentorQuery(string? Search, int? DomainId, bool? HasCapacity, bool? IsInternal, int Page = 1, int PageSize = 20);

public sealed partial class MentorInputValidator : AbstractValidator<MentorInput>
{
    public MentorInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nhập họ tên.").MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Nhập email.")
            .Must(e => e!.Trim().Length <= 254 && EmailPattern().IsMatch(e.Trim())).When(x => !string.IsNullOrWhiteSpace(x.Email), ApplyConditionTo.CurrentValidator).WithMessage("Email không đúng định dạng.");
        RuleFor(x => x.Organization).MaximumLength(150).WithMessage("Tổ chức tối đa 150 ký tự.");
        RuleFor(x => x.MaxTeams).NotNull().WithMessage("Nhập số nhóm tối đa.")
            .InclusiveBetween(MentorRules.MinTeams, MentorRules.MaxTeams).WithMessage($"Số nhóm tối đa từ {MentorRules.MinTeams} đến {MentorRules.MaxTeams}.");
        RuleFor(x => x.DomainIds).Must(ids => ids!.Count <= LecturerRules.MaxDomains).When(x => x.DomainIds is not null)
            .WithMessage($"Tối đa {LecturerRules.MaxDomains} domain.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

public sealed class MentorProfileInputValidator : AbstractValidator<MentorProfileInput>
{
    public MentorProfileInputValidator()
    {
        RuleFor(x => x.PreferredTeams).GreaterThanOrEqualTo(MentorRules.MinTeams).WithMessage($"Số nhóm mong muốn tối thiểu {MentorRules.MinTeams}.");
        RuleFor(x => x.Description).MaximumLength(MentorRules.MaxDescription).WithMessage($"Giới thiệu tối đa {MentorRules.MaxDescription} ký tự.");
        RuleFor(x => x.DomainIds).Must(ids => ids!.Count <= LecturerRules.MaxDomains).When(x => x.DomainIds is not null)
            .WithMessage($"Tối đa {LecturerRules.MaxDomains} domain.");
    }
}

public sealed class MentorQueryValidator : AbstractValidator<MentorQuery>
{
    public MentorQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page bắt đầu từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize từ 1 đến 100.");
    }
}

public static class MentorRules
{
    public const int MinTeams = 1;

    /// <summary><c>ck_mentors_max_teams</c>.</summary>
    public const int MaxTeams = 50;

    public const int MaxDescription = 500;

    /// <summary>
    /// Bộ môn hạ số nhóm tối đa xuống dưới số mong muốn thì số mong muốn hạ theo (<c>ck_mentors_preferred_teams</c>):
    /// tối đa do Bộ môn quyết định, mentor chỉnh lại trong giới hạn mới.
    /// </summary>
    public static short? CapPreferred(short? preferredTeams, short maxTeams) =>
        preferredTeams > maxTeams ? maxTeams : preferredTeams;
}

// Payload sự kiện exe.student (docs/contracts/events.md), key = mentorId.
// Có thêm domainIds cho read model của Matching (Matching đã đọc trường này) – đề xuất sửa events.md.

public static class MentorEventTypes
{
    public const string MentorRegistered = "MentorRegistered";
}

public sealed record MentorRegisteredEvent(
    Guid MentorId, string FullName, string Email, string? Organization, bool IsInternal, Guid? LecturerId,
    IReadOnlyList<int> DomainIds, int MaxTeams, bool IsActive);
