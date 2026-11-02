using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Teams;

namespace Team.Tests;

/// <summary>Câu đếm cờ <c>capacityReached</c> (BE-46) trên team_db thật; bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class LecturerPreferenceCapacityDbTests
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
    public async Task CountsAssignmentsOfSemesterAndCourse_ExcludingDisbandedTeams()
    {
        await using var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql(TeamApiFactory.ConnectionString).UseSnakeCaseNamingConvention().Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var semester = Guid.NewGuid();
        var lecturer = Guid.NewGuid();
        var teams = new[]
        {
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE201", Name = "Nhóm đang hướng dẫn", Status = "ACTIVE" },
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE201", Name = "Nhóm cần xem lại", Status = "NEEDS_REVIEW" },
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE201", Name = "Nhóm giải tán", Status = "DISBANDED" },
            new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester, CourseCode = "EXE101", Name = "Nhóm môn khác", Status = "ACTIVE" },
        };
        db.AddRange(teams);
        await db.SaveChangesAsync();
        foreach (var team in teams)
        {
            db.Add(new LecturerAssignment { Id = Guid.NewGuid(), TeamId = team.Id, LecturerId = lecturer, LecturerName = "GV thử", Reason = "DOMAIN" });
        }
        await db.SaveChangesAsync();

        var counts = await new LecturerPreferenceRepository(db)
            .CountActiveAssignmentsAsync(semester, "EXE201", [lecturer, Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(2, Assert.Single(counts).Value);
        await transaction.RollbackAsync();
    }
}
