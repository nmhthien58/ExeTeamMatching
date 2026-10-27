using System.Text.RegularExpressions;
using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Academic.Tests;

/// <summary>Mỗi entity phải map đúng tên bảng và đủ cột như deploy/db/02_academic_db.sql (không cần DB).</summary>
public sealed class AcademicDbContextModelTests
{
    private static AcademicDbContext NewDb() => new(new DbContextOptionsBuilder<AcademicDbContext>()
        .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);

    [Fact]
    public void Db_script_has_the_15_expected_tables()
    {
        Assert.Equal(new HashSet<string>
        {
            "specializations", "domains", "class_codes", "semesters", "registration_rounds", "students", "student_skills",
            "lecturers", "lecturer_domains", "mentors", "mentor_domains", "import_batches", "semester_enrollments",
            "rule_settings", "outbox_messages",
        }, ReadScriptTables().Keys.ToHashSet());
    }

    [Fact]
    public void Every_table_of_the_db_script_is_mapped()
    {
        using var db = NewDb();

        var mapped = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).ToHashSet();
        Assert.Empty(ReadScriptTables().Keys.Except(mapped));
    }

    [Fact]
    public void Every_mapped_table_has_exactly_the_columns_of_the_db_script()
    {
        var scriptTables = ReadScriptTables();
        using var db = NewDb();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            Assert.Equal(AcademicDbContext.Schema, entity.GetSchema());
            Assert.True(scriptTables.TryGetValue(table, out var expected), $"Bảng {table} không có trong DB script.");

            var store = StoreObjectIdentifier.Table(table, AcademicDbContext.Schema);
            var actual = entity.GetProperties().Select(p => p.GetColumnName(store)!).ToHashSet();
            Assert.True(expected.SetEquals(actual),
                $"{table}: thiếu [{string.Join(", ", expected.Except(actual))}], thừa [{string.Join(", ", actual.Except(expected))}]");
        }
    }

    [Fact]
    public void Citext_and_jsonb_columns_use_the_db_types()
    {
        using var db = NewDb();

        var columns = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => (Table: e.GetTableName(), Column: p.GetColumnName(), Type: p.GetColumnType())))
            .Where(c => c.Type is "citext" or "jsonb")
            .Select(c => $"{c.Table}.{c.Column}:{c.Type}")
            .ToHashSet();

        Assert.Superset(new HashSet<string>
        {
            "students.email:citext", "lecturers.email:citext", "mentors.email:citext", "import_batches.errors:jsonb",
            "outbox_messages.payload:jsonb",
        }, columns);
    }

    private static Dictionary<string, HashSet<string>> ReadScriptTables()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "deploy", "db", "02_academic_db.sql")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var sql = File.ReadAllText(Path.Combine(dir.FullName, "deploy", "db", "02_academic_db.sql"));

        return Regex.Matches(sql, @"CREATE TABLE academic\.(\w+) \((.*?)\r?\n\);", RegexOptions.Singleline)
            .ToDictionary(
                m => m.Groups[1].Value,
                m => Regex.Matches(m.Groups[2].Value, @"^\s{4}([a-z_0-9]+)\s+(?:uuid|varchar|smallint|integer|bigint|boolean|timestamptz|date|citext|text|jsonb|numeric)", RegexOptions.Multiline)
                    .Select(c => c.Groups[1].Value)
                    .ToHashSet());
    }
}
