using FluentValidation;

namespace Team.Services.Finalization;

// Schema của team.yaml (tag Admin, Internal) và payload exe.team (docs/contracts/events.md) của module Finalization.

public static class FinalizationEventTypes
{
    public const string TeamFinalized = "TeamFinalized";
    public const string TeamNeedsReview = "TeamNeedsReview";
}

/// <summary><c>FinalizedBy</c>: SYSTEM (đóng đợt, ghép tự động) | ADMIN.</summary>
public sealed record TeamFinalized(Guid TeamId, Guid SemesterId, IReadOnlyList<Guid> MemberStudentIds, string FinalizedBy);

public sealed record TeamNeedsReview(Guid TeamId, Guid SemesterId, IReadOnlyList<string> Violations);

/// <summary>Body của <c>POST /internal/teams/finalize-round</c>.</summary>
public sealed record FinalizeRoundRequest(Guid RoundId, Guid SemesterId, string CourseCode);

public sealed record FinalizeRoundResult(int FinalizedCount, int NeedsReviewCount);

/// <summary>Body của <c>POST /api/teams/{teamId}/members</c> (W16, W21).</summary>
public sealed record AdminAddMemberRequest(Guid StudentId, string? Reason);

/// <summary>Body của <c>POST /api/teams/{teamId}/members/{studentId}/move</c> (W21).</summary>
public sealed record MoveMemberRequest(Guid TargetTeamId);

public sealed class FinalizeRoundRequestValidator : AbstractValidator<FinalizeRoundRequest>
{
    public FinalizeRoundRequestValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(c => c is "EXE101" or "EXE201").WithMessage("courseCode phải là EXE101 hoặc EXE201.");
    }
}

public sealed class AdminAddMemberRequestValidator : AbstractValidator<AdminAddMemberRequest>
{
    public AdminAddMemberRequestValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}

public sealed class MoveMemberRequestValidator : AbstractValidator<MoveMemberRequest>
{
    public MoveMemberRequestValidator() => RuleFor(x => x.TargetTeamId).NotEmpty();
}
