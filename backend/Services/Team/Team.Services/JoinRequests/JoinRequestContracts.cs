using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.JoinRequests;

/// <summary>Schema <c>Idea</c> (rule 17).</summary>
public sealed record IdeaDto(string Title, string? Description);

public sealed record SkillRating(string Skill, int Level);

/// <summary>
/// Schema <c>JoinRequest</c> (S09, S12, S20). <c>studentSkills</c>, <c>studentDescription</c> lấy từ hồ sơ của Academic
/// (<c>POST /internal/students/lookup</c>); rỗng khi sinh viên chưa khai báo hoặc Academic lỗi.
/// </summary>
public sealed record JoinRequestResponse(
    Guid Id, TeamCardResponse? Team, StudentRef Student, IReadOnlyList<SkillRating> StudentSkills, string? StudentDescription,
    string? Message, IReadOnlyList<IdeaDto> Ideas, bool HasNoIdea, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record SubmitJoinRequest(string? Message, IReadOnlyList<IdeaDto>? Ideas, bool HasNoIdea);

public sealed class SubmitJoinRequestValidator : AbstractValidator<SubmitJoinRequest>
{
    public SubmitJoinRequestValidator()
    {
        RuleFor(x => x.Message).MaximumLength(500).WithMessage("Lời nhắn tối đa 500 ký tự.");
        RuleFor(x => x.Ideas).Must(i => i is null || i.Count <= 3).WithMessage("Tối đa 3 ý tưởng.");
        RuleForEach(x => x.Ideas).ChildRules(idea =>
        {
            idea.RuleFor(i => i.Title)
                .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Nhập tên ý tưởng.")
                .MaximumLength(80).WithMessage("Tên ý tưởng tối đa 80 ký tự.");
            idea.RuleFor(i => i.Description).MaximumLength(300).WithMessage("Mô tả ý tưởng tối đa 300 ký tự.");
        });
    }
}

// Payload exe.invitation (docs/contracts/events.md), key = joinRequestId.

public static class JoinRequestEventTypes
{
    public const string JoinRequestSubmitted = "JoinRequestSubmitted";
    public const string JoinRequestAccepted = "JoinRequestAccepted";
    public const string JoinRequestDeclined = "JoinRequestDeclined";
}

/// <summary><c>IdeaCount</c> = 0 khi <c>HasNoIdea</c>.</summary>
public sealed record JoinRequestSubmitted(
    Guid JoinRequestId, Guid TeamId, Guid StudentId, Guid LeaderStudentId, DateTimeOffset ExpiresAt, int IdeaCount, bool HasNoIdea);

/// <summary>Dùng cho JoinRequestAccepted / JoinRequestDeclined.</summary>
public sealed record JoinRequestResolved(Guid JoinRequestId, Guid TeamId, Guid StudentId);
