using Academic.Repositories.Semesters;
using FluentValidation;

namespace Academic.Services.Semesters;

// Schema của academic.yaml (tag Semesters). JSON camelCase, ngày dạng yyyy-MM-dd.

/// <summary>Schema <c>Semester</c>.</summary>
public sealed record SemesterResponse(
    Guid Id, string Code, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent,
    int MentoringStartWeek, int ExpectedMentorSessions, int SlotBookingDeadlineWeek, int MeetingSlotMinutes,
    IReadOnlyList<int>? CheckpointTargetWeeks)
{
    public static SemesterResponse From(Semester s) => new(
        s.Id, s.Code, s.Name, s.StartDate, s.EndDate, s.IsCurrent,
        s.MentoringStartWeek, s.ExpectedMentorSessions, s.SlotBookingDeadlineWeek, s.MeetingSlotMinutes,
        s.CheckpointTargetWeeks?.Select(w => (int)w).ToList());
}

/// <summary>
/// Schema <c>SemesterInput</c>. Trường số bỏ trống: thêm mới lấy mặc định của DB, sửa thì giữ nguyên.
/// <see cref="IsCurrent"/> = true thì học kỳ đang hiện hành khác tự bỏ cờ. <see cref="CheckpointTargetWeeks"/> luôn ghi đè (null = không nhắc).
/// </summary>
public sealed record SemesterInput(
    string? Code, string? Name, DateOnly? StartDate, DateOnly? EndDate, bool? IsCurrent,
    int? MentoringStartWeek, int? ExpectedMentorSessions, int? SlotBookingDeadlineWeek, int? MeetingSlotMinutes,
    IReadOnlyList<int>? CheckpointTargetWeeks);

public sealed class SemesterInputValidator : AbstractValidator<SemesterInput>
{
    public SemesterInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Nhập mã học kỳ.")
            .Matches("^(SP|SU|FA)[0-9]{2}$").WithMessage("Mã học kỳ dạng SP26, SU26, FA26.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên học kỳ.")
            .MaximumLength(50).WithMessage("Tên tối đa 50 ký tự.");
        RuleFor(x => x.StartDate).NotNull().WithMessage("Chọn ngày bắt đầu.");
        RuleFor(x => x.EndDate).NotNull().WithMessage("Chọn ngày kết thúc.")
            .GreaterThan(x => x.StartDate).When(x => x.StartDate is not null).WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
        RuleFor(x => x.MentoringStartWeek).InclusiveBetween(1, 20).WithMessage("Tuần bắt đầu mentor từ 1 đến 20.");
        RuleFor(x => x.ExpectedMentorSessions).InclusiveBetween(1, 10).WithMessage("Số buổi mentor từ 1 đến 10.");
        RuleFor(x => x.SlotBookingDeadlineWeek).InclusiveBetween(1, 10).WithMessage("Hạn chọn slot họp từ tuần 1 đến 10.");
        RuleFor(x => x.MeetingSlotMinutes).InclusiveBetween(5, 60).WithMessage("Một slot họp dài 5–60 phút.");
        RuleFor(x => x.CheckpointTargetWeeks)
            .Must(w => w!.Count == 4 && w.All(n => n is >= 1 and <= 20)).When(x => x.CheckpointTargetWeeks is not null)
            .WithMessage("Nhập đủ 4 tuần Checkpoint, mỗi tuần từ 1 đến 20.");
    }
}
