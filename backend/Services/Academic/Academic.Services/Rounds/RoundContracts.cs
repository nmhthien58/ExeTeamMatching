using Academic.Repositories.Rounds;
using FluentValidation;

namespace Academic.Services.Rounds;

// Schema của academic.yaml (tag Rounds). JSON camelCase, thời gian UTC.

/// <summary>Schema <c>Round</c>.</summary>
public sealed record RoundResponse(
    Guid Id, Guid SemesterId, string CourseCode, string RoundType, string Name,
    DateTimeOffset OpensAt, DateTimeOffset DeadlineAt, string Status, DateTimeOffset? ClosedAt)
{
    public static RoundResponse From(RegistrationRound r) =>
        new(r.Id, r.SemesterId, r.CourseCode, r.RoundType, r.Name, r.OpensAt, r.DeadlineAt, r.Status, r.ClosedAt);
}

/// <summary>Schema <c>RoundInput</c>.</summary>
public sealed record RoundInput(
    Guid? SemesterId, string? CourseCode, string? RoundType, string? Name, DateTimeOffset? OpensAt, DateTimeOffset? DeadlineAt);

/// <summary>Query của <c>GET /api/registration-rounds</c>.</summary>
public sealed record RoundQuery(Guid? SemesterId, string? CourseCode, string? Status);

public sealed class RoundInputValidator : AbstractValidator<RoundInput>
{
    private static readonly string[] Courses = [CourseCodes.Exe101, CourseCodes.Exe201];
    private static readonly string[] Types = [RoundTypes.TeamFormation, RoundTypes.ChangeRequest];

    public RoundInputValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage("Chọn học kỳ.");
        RuleFor(x => x.CourseCode).Must(c => Courses.Contains(c)).WithMessage("Môn là EXE101 hoặc EXE201.");
        RuleFor(x => x.RoundType).Must(t => Types.Contains(t)).WithMessage("Loại đợt là TEAM_FORMATION hoặc CHANGE_REQUEST.");
        RuleFor(x => x.RoundType).Must((x, t) => t != RoundTypes.ChangeRequest || x.CourseCode == CourseCodes.Exe201)
            .WithMessage("Đợt đổi nhóm chỉ có ở EXE201.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên đợt.").MaximumLength(100).WithMessage("Tên tối đa 100 ký tự.");
        RuleFor(x => x.OpensAt).NotNull().WithMessage("Chọn giờ mở.");
        RuleFor(x => x.DeadlineAt).NotNull().WithMessage("Chọn hạn chót.")
            .GreaterThan(x => x.OpensAt).When(x => x.OpensAt is not null).WithMessage("Hạn chót phải sau giờ mở.");
    }
}

public sealed class RoundQueryValidator : AbstractValidator<RoundQuery>
{
    public RoundQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null or CourseCodes.Exe101 or CourseCodes.Exe201).WithMessage("Môn là EXE101 hoặc EXE201.");
        RuleFor(x => x.Status).Must(s => s is null or RoundStatuses.Scheduled or RoundStatuses.Open or RoundStatuses.Closed)
            .WithMessage("Trạng thái là SCHEDULED, OPEN hoặc CLOSED.");
    }
}

// Payload sự kiện exe.registration (docs/contracts/events.md), key = roundId.

public static class RoundEventTypes
{
    public const string RoundOpened = "RoundOpened";
    public const string RoundClosed = "RoundClosed";
}

public sealed record RoundEvent(Guid RoundId, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset OpensAt, DateTimeOffset DeadlineAt)
{
    public static RoundEvent From(RegistrationRound r) => new(r.Id, r.SemesterId, r.CourseCode, r.RoundType, r.OpensAt, r.DeadlineAt);
}
