using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;

namespace Team.Tests;

public sealed class TopicInterestCapacityDbTests
{
    private sealed class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("EXE_INTEGRATION") != "1")
                Skip = "Cần PostgreSQL thử nghiệm với EXE_INTEGRATION=1.";
        }
    }

    [IntegrationFact]
    public async Task CountsAssignmentsInCurrentSemesterAndLecturerCourse()
    {
        await using var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql(TeamApiFactory.ConnectionString).UseSnakeCaseNamingConvention().Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var semester = Guid.NewGuid();
        var previousSemester = Guid.NewGuid();
        var advisor = Guid.NewGuid();
        var teams = new[]
        {
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE101", Name = "Nhóm thử 1" },
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE201", Name = "Nhóm thử 2" },
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = previousSemester, CourseCode = "EXE201", Name = "Nhóm thử 3" },
        };
        db.AddRange(teams);
        await db.SaveChangesAsync();
        foreach (var team in teams)
        {
            db.Add(new LecturerAssignment { Id = Guid.NewGuid(), TeamId = team.Id, LecturerId = advisor, LecturerName = "GV thử", Reason = "DOMAIN" });
            db.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = team.Id, MentorId = advisor, MentorName = "Mentor thử", Reason = "DOMAIN" });
        }
        await db.SaveChangesAsync();

        var repo = new TopicInterestRepository(db);
        Assert.Equal(1, await repo.CountActiveAssignmentsByAdvisorAsync("LECTURER", advisor, semester, "EXE201", CancellationToken.None));
        Assert.Equal(2, await repo.CountActiveAssignmentsByAdvisorAsync("MENTOR", advisor, semester, "EXE201", CancellationToken.None));
        await transaction.RollbackAsync();
    }
}
