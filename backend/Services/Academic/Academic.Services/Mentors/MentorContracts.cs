using System.Text.RegularExpressions;
using Academic.Services.Lecturers;
using BuildingBlocks.Errors;
using FluentValidation;

namespace Academic.Services.Mentors;

// Schema của academic.yaml (tag Mentors). JSON camelCase.

/// <summary>
/// Schema <c>Mentor</c>. <see cref="CurrentTeamCount"/>: số nhóm đang mentor trong học kỳ hiện hành, lấy từ Team Service
/// (<c>POST /internal/mentors/team-counts</c>); Team không trả lời thì 0.
/// </summary>
public sealed record MentorResponse(
    Guid Id, string FullName, string Email, string? Organization, bool IsInternal, Guid? LecturerId, string? Description,
    IReadOnlyList<CatalogRef> Domains, int MaxTeams, int? PreferredTeams, int CurrentTeamCount, bool IsActive);

/// <summary>
/// Schema <c>MentorInput</c>. Khi sửa, <see cref="IsInternal"/>, <see cref="IsActive"/>, <see cref="DomainIds"/> bỏ trống thì giữ nguyên;
/// <see cref="LecturerId"/> bỏ trống cũng giữ liên kết cũ, muốn gỡ thì đặt <see cref="IsInternal"/> = false.
/// Mentor gắn giảng viên dùng email giảng viên nên <see cref="Email"/> không bắt buộc và bị bỏ qua; mentor không gắn giảng viên thì bắt buộc,
/// service kiểm tra vì chỉ service biết liên kết cũ (<see cref="MentorRules.RequireEmail"/>).
/// </summary>
public sealed record MentorInput(
    string? FullName, string? Email, string? Organization, bool? IsInternal, Guid? LecturerId, IReadOnlyList<int>? DomainIds,
    int? MaxTeams, bool? IsActive);

/// <summary>
/// Body của <c>PUT /api/mentors/me</c>. Trường bỏ trống thì giữ nguyên; <see cref="Description"/> là chuỗi rỗng thì xóa giới thiệu.
/// </summary>
public sealed record MentorProfileInput(int? PreferredTeams, string? Description, IReadOnlyList<int>? DomainIds);

/// <summary>
/// Query của <c>GET /api/mentors</c>. <see cref="HasCapacity"/>: true = đang mentor &lt; tối đa, false = đã đủ; lọc trên toàn bộ
/// dữ liệu trước khi phân trang. Team không trả lời thì bỏ qua bộ lọc này.
/// </summary>
public sealed record MentorQuery(string? Search, int? DomainId, bool? HasCapacity, bool? IsInternal, int Page = 1, int PageSize = 20);

public sealed class MentorInputValidator : AbstractValidator<MentorInput>
{
    public MentorInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nhập họ tên.").MaximumLength(150).WithMessage("Họ tên tối đa 150 ký tự.");
        RuleFor(x => x.Organization).MaximumLength(150).WithMessage("Tổ chức tối đa 150 ký tự.");
        RuleFor(x => x.MaxTeams).NotNull().WithMessage("Nhập số nhóm tối đa.")
            .InclusiveBetween(MentorRules.MinTeams, MentorRules.MaxTeams).WithMessage($"Số nhóm tối đa từ {MentorRules.MinTeams} đến {MentorRules.MaxTeams}.");
        RuleFor(x => x.DomainIds).Must(ids => ids!.Count <= LecturerRules.MaxDomains).When(x => x.DomainIds is not null)
            .WithMessage($"Tối đa {LecturerRules.MaxDomains} domain.");
    }
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

public static partial class MentorRules
{
    public const int MinTeams = 1;

    /// <summary><c>ck_mentors_max_teams</c>.</summary>
    public const int MaxTeams = 50;

    public const int MaxDescription = 500;

    /// <summary>Email của mentor không gắn giảng viên: bắt buộc, đúng định dạng (<c>ck_mentors_email</c>). Sai → 400 theo field <c>email</c>.</summary>
    public static string RequireEmail(string? email)
    {
        var value = email?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw InvalidEmail("Nhập email.");
        }
        return value.Length <= 254 && EmailPattern().IsMatch(value) ? value : throw InvalidEmail("Email không đúng định dạng.");
    }

    private static RequestValidationException InvalidEmail(string message) =>
        new(new Dictionary<string, string[]> { ["email"] = [message] });

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    /// <summary>
    /// Bộ môn hạ số nhóm tối đa xuống dưới số mong muốn thì số mong muốn hạ theo (<c>ck_mentors_preferred_teams</c>):
    /// tối đa do Bộ môn quyết định, mentor chỉnh lại trong giới hạn mới.
    /// </summary>
    public static short? CapPreferred(short? preferredTeams, short maxTeams) =>
        preferredTeams > maxTeams ? maxTeams : preferredTeams;
}

// Payload sự kiện exe.student (docs/contracts/events.md), key = mentorId.

public static class MentorEventTypes
{
    public const string MentorRegistered = "MentorRegistered";
}

public sealed record MentorRegisteredEvent(
    Guid MentorId, string FullName, string Email, string? Organization, bool IsInternal, Guid? LecturerId,
    IReadOnlyList<int> DomainIds, int MaxTeams, bool IsActive);
