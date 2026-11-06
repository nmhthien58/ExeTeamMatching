using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Projects;

namespace Team.Tests;

public sealed class ProjectLineageTests
{
    [Fact]
    public void Manual_link_requires_unlinked_inherited_exe201_team_and_matching_exe101_source()
    {
        var firstSemester = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), OriginSemesterId = firstSemester, OriginCourseCode = "EXE101" };
        var source = new TeamEntity { Id = Guid.NewGuid(), ProjectId = project.Id, SemesterId = firstSemester, CourseCode = "EXE101" };
        var target = new TeamEntity { SemesterId = Guid.NewGuid(), CourseCode = "EXE201", IsInherited = true };

        Assert.True(ProjectLineage.CanLink(project, source, target));
        target.ProjectId = Guid.NewGuid();
        Assert.False(ProjectLineage.CanLink(project, source, target));
        target.ProjectId = null;
        target.IsInherited = false;
        Assert.False(ProjectLineage.CanLink(project, source, target));
        target.IsInherited = true;
        source.ProjectId = Guid.NewGuid();
        Assert.False(ProjectLineage.CanLink(project, source, target));
    }

    [Fact]
    public void Retained_members_use_previous_team_as_denominator_and_ignore_code_case()
    {
        Assert.Equal(50, ProjectLineage.KeptMemberPercent(["SE001", "SE002", "SE003", "SE004"],
            ["se001", "SE002", "SE005", "SE006"]));
        Assert.Null(ProjectLineage.KeptMemberPercent([], ["SE005"]));
    }
}
