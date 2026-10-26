using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Team.Repositories.Context;

namespace Team.Tests;

public sealed class TeamDbErrorsTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_team_members_semester_student", "already-in-team")]
    [InlineData(PostgresErrorCodes.CheckViolation, "ck_teams_member_count", "team-full")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_invitations_pending", "pending-exists")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_join_requests_pending", "pending-exists")]
    [InlineData(PostgresErrorCodes.CheckViolation, "ck_team_finalized_size", "composition-invalid")]
    [InlineData(PostgresErrorCodes.CheckViolation, "ck_join_requests_ideas", "ideas-required")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_lecturer_assignments_active", "team-has-advisor")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_mentor_assignments_active", "team-has-advisor")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_teams_semester_name", "constraint-violation")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "fk_invitations_team", "constraint-violation")]
    public void Constraint_violation_becomes_409_with_contract_code(string sqlState, string constraint, string code)
    {
        var pg = Postgres(sqlState, constraint);

        // Lỗi lúc ghi lệnh (bọc trong DbUpdateException) và lỗi trigger lúc COMMIT (không bọc) đều được đổi.
        foreach (var ex in new Exception[] { new DbUpdateException("save", pg), pg })
        {
            var mapped = TeamDbErrors.ToAppException(ex);
            Assert.NotNull(mapped);
            Assert.Equal(409, mapped.Status);
            Assert.Equal(code, mapped.Code);
        }
    }

    [Fact]
    public void Concurrency_exception_becomes_concurrency_conflict()
    {
        var mapped = TeamDbErrors.ToAppException(new DbUpdateConcurrencyException("xmin"));
        Assert.IsType<ConflictException>(mapped);
        Assert.Equal("concurrency-conflict", mapped.Code);
    }

    [Fact]
    public void Other_errors_are_not_mapped()
    {
        Assert.Null(TeamDbErrors.ToAppException(Postgres(PostgresErrorCodes.InsufficientPrivilege, "ck_audit_logs_append_only")));
        Assert.Null(TeamDbErrors.ToAppException(new InvalidOperationException()));
    }

    private static PostgresException Postgres(string sqlState, string constraint) =>
        new("lỗi thử", "ERROR", "ERROR", sqlState, constraintName: constraint);
}
