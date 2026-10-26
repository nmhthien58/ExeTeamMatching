using Academic.Repositories.Catalogs;
using Academic.Repositories.Context;
using Academic.Repositories.Lecturers;
using Academic.Repositories.Mentors;
using Academic.Repositories.Settings;
using Academic.Repositories.Students;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// Chạy với academic_db thật (DB script + seed + trigger). Bật bằng <c>EXE_INTEGRATION=1</c> sau
/// <c>docker compose -f deploy/docker-compose.yml up -d postgres</c>; chuỗi kết nối đổi được qua <c>EXE_ACADEMIC_DB</c>.
/// Test ghi dữ liệu dùng email / MSSV ngẫu nhiên và xóa lại sau khi xong.
/// </summary>
public sealed class AcademicDbIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private static AcademicDbContext NewDb() => new(new DbContextOptionsBuilder<AcademicDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("EXE_ACADEMIC_DB")
            ?? "Host=localhost;Port=5432;Database=academic_db;Username=academic_svc;Password=academic_dev_pw")
        .UseSnakeCaseNamingConvention().Options);

    [Fact]
    public async Task Seed_catalogs_are_readable()
    {
        if (!Enabled)
        {
            return;
        }

        await using var db = NewDb();
        Assert.Equal(18, await db.Domains.CountAsync());
        Assert.True(await db.Specializations.AnyAsync());
        Assert.True(await db.ClassCodes.AnyAsync());
        Assert.True(await db.Semesters.AnyAsync(s => s.IsCurrent));

        var rules = await db.RuleSettings.SingleAsync();
        Assert.Equal(RuleSetting.SingletonId, rules.Id);
        Assert.NotEmpty(rules.ReminderHours);
    }

    [Fact]
    public async Task Student_with_skills_saves_and_trigger_sets_updated_at()
    {
        if (!Enabled)
        {
            return;
        }

        await using var db = NewDb();
        var specialization = await db.Specializations.FirstAsync();
        var code = $"ZZ{Random.Shared.Next(0, 1_000_000):D6}";
        var student = new Student
        {
            StudentCode = code,
            FullName = "Sinh Viên Thử",
            Email = $"{code.ToLowerInvariant()}@example.test",
            SpecializationId = specialization.Id,
        };
        db.Students.Add(student);
        db.StudentSkills.Add(new StudentSkill { StudentId = student.Id, Skill = "TECHNOLOGY", Level = 4 });

        try
        {
            await db.SaveChangesAsync();
            var created = student.UpdatedAt;
            Assert.NotEqual(default, student.CreatedAt);

            student.Description = "Đã sửa";
            await db.SaveChangesAsync();
            Assert.True(student.UpdatedAt >= created);

            await using var check = NewDb();
            // citext: tìm theo email viết hoa vẫn ra.
            var saved = await check.Students.SingleAsync(s => s.Email == student.Email.ToUpperInvariant());
            Assert.Equal("Đã sửa", saved.Description);
            Assert.Equal(4, (await check.StudentSkills.SingleAsync(s => s.StudentId == student.Id)).Level);
        }
        finally
        {
            await using var cleanup = NewDb();
            await cleanup.Students.Where(s => s.Id == student.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Mentor_linked_to_lecturer_and_domains_save_in_order()
    {
        if (!Enabled)
        {
            return;
        }

        await using var db = NewDb();
        var domain = await db.Domains.FirstAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lecturer = new Lecturer { Id = Guid.NewGuid(), FullName = "Giảng Viên Thử", Email = $"gv-{suffix}@example.test" };
        var mentor = new Mentor
        {
            Id = Guid.NewGuid(),
            FullName = "Giảng Viên Thử",
            Email = $"mentor-{suffix}@example.test",
            IsInternal = true,
            LecturerId = lecturer.Id,
            MaxTeams = 3,
            CreatedBy = Guid.NewGuid(),
        };
        // Thêm con trước cha: EF phải tự sắp thứ tự theo khóa ngoại.
        db.MentorDomains.Add(new MentorDomain { MentorId = mentor.Id, DomainId = domain.Id });
        db.LecturerDomains.Add(new LecturerDomain { LecturerId = lecturer.Id, DomainId = domain.Id });
        db.Mentors.Add(mentor);
        db.Lecturers.Add(lecturer);

        try
        {
            await db.SaveChangesAsync();

            await using var check = NewDb();
            Assert.Equal(lecturer.Id, (await check.Mentors.SingleAsync(m => m.Id == mentor.Id)).LecturerId);
            Assert.True(await check.MentorDomains.AnyAsync(m => m.MentorId == mentor.Id && m.DomainId == domain.Id));
            Assert.True(await check.LecturerDomains.AnyAsync(l => l.LecturerId == lecturer.Id));
        }
        finally
        {
            await using var cleanup = NewDb();
            await cleanup.Mentors.Where(m => m.Id == mentor.Id).ExecuteDeleteAsync();
            await cleanup.Lecturers.Where(l => l.Id == lecturer.Id).ExecuteDeleteAsync();
        }
    }
}
