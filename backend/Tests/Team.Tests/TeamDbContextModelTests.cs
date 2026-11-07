using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Team.Repositories.Context;

namespace Team.Tests;

/// <summary>Mỗi entity phải map đúng tên bảng và đủ cột như deploy/db/03_team_db.sql (không cần DB).</summary>
public sealed class TeamDbContextModelTests
{
    [Fact]
    public void Every_table_of_the_db_script_is_mapped()
    {
        using var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);

        var mapped = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).ToHashSet();
        Assert.Empty(ReadScriptTables().Keys.Except(mapped));
    }

    [Fact]
    public void Every_mapped_table_has_exactly_the_columns_of_the_db_script()
    {
        var scriptTables = ReadScriptTables();
        using var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            Assert.Equal(TeamDbContext.Schema, entity.GetSchema());
            Assert.True(scriptTables.TryGetValue(table, out var expected), $"Bảng {table} không có trong DB script.");

            var store = StoreObjectIdentifier.Table(table, TeamDbContext.Schema);
            var actual = entity.GetProperties().Select(p => p.GetColumnName(store)!).Where(c => c != "xmin").ToHashSet();
            Assert.True(expected.SetEquals(actual),
                $"{table}: thiếu [{string.Join(", ", expected.Except(actual))}], thừa [{string.Join(", ", actual.Except(expected))}]");
        }
    }

    [Fact]
    public void Mutable_tables_use_xmin_as_concurrency_token()
    {
        using var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);

        var withXmin = db.Model.GetEntityTypes()
            .Where(e => e.GetProperties().Any(p => p.IsConcurrencyToken && p.GetColumnName() == "xmin"))
            .Select(e => e.GetTableName())
            .ToHashSet();

        Assert.Equal(new HashSet<string?>
        {
            "teams", "team_members", "invitations", "join_requests", "recruitment_posts", "seeking_posts",
            "projects", "change_requests", "topic_interests", "advisor_matching_runs", "lecturer_assignments", "mentor_assignments",
        }, withXmin);
    }

    private static Dictionary<string, HashSet<string>> ReadScriptTables()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "deploy", "db", "03_team_db.sql")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var sql = File.ReadAllText(Path.Combine(dir.FullName, "deploy", "db", "03_team_db.sql"))
            + File.ReadAllText(Path.Combine(dir.FullName, "deploy", "db", "migrations", "BE-88_leader_votes.sql"));

        return Regex.Matches(sql, @"CREATE TABLE (?:IF NOT EXISTS )?team\.(\w+) \((.*?)\r?\n\);", RegexOptions.Singleline)
            .ToDictionary(
                m => m.Groups[1].Value,
                m => Regex.Matches(m.Groups[2].Value, @"^\s{4}([a-z_]+)\s+(?:uuid|varchar|smallint|integer|bigint|boolean|timestamptz|text|jsonb|numeric)", RegexOptions.Multiline)
                    .Select(c => c.Groups[1].Value)
                    .ToHashSet());
    }
}
