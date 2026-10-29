using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.Projects;

public sealed record ProjectQuery(Guid? SemesterId = null, string? Search = null, string? Domain = null,
    bool PivotedOnly = false, int Page = 1, int PageSize = 20);

public sealed class ProjectQueryValidator : AbstractValidator<ProjectQuery>
{
    public ProjectQueryValidator()
    {
        RuleFor(x => x.Domain).Matches("^D(0[1-9]|1[0-8])$").When(x => x.Domain is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record LinkTeamInput(Guid TeamId);
public sealed record ProjectVersionResponse(int Version, string Name, string? Description, string ChangeType,
    string? Reason, string? SemesterCode, string CourseCode, string? ChangedByName, DateTimeOffset CreatedAt);
public sealed record ProjectTeamResponse(TeamCardResponse Team, string? SemesterCode, double? KeptMemberPercent,
    string? InstructorName, string? MentorName);
public sealed record ProjectDetailResponse(Guid Id, string Name, string? Description, int CurrentVersion,
    string OriginCourseCode, string? OriginSemesterCode, IReadOnlyList<ProjectVersionResponse> Versions,
    IReadOnlyList<ProjectTeamResponse> Teams);
