using Matching.Services.Events;
using Matching.Services.ReadModel;

namespace Matching.Tests;

/// <summary>Dựng dữ liệu test bằng chính sự kiện Kafka, qua <see cref="ReadModelProjector"/>.</summary>
public sealed class Scenario
{
    public const int SE = 1;
    public const int GD = 5;
    public const int DM = 7;
    public const int IB = 8;

    private static readonly CancellationToken Ct = CancellationToken.None;
    private int _studentNo;

    public InMemoryMatchingReadModel Model { get; } = new();
    public ReadModelProjector Projector => new(Model);
    public Guid SemesterId { get; } = Guid.NewGuid();

    /// <summary>Sinh viên đã import; có <paramref name="skills"/> thì coi như đã hoàn thiện hồ sơ.</summary>
    public async Task<Guid> StudentAsync(int specializationId, string course = Courses.Exe101, params (Skill Skill, int Level)[] skills)
    {
        var id = Guid.NewGuid();
        var name = $"SV {++_studentNo:000}";
        await Projector.HandleAsync(new StudentImported(id, name, specializationId, SemesterId, course), Ct);
        if (skills.Length > 0)
        {
            await Projector.HandleAsync(new StudentProfileUpdated(id, name, specializationId,
                skills.Select(s => new SkillRatingPayload(SkillCode(s.Skill), s.Level)).ToList(), IsSeekingTeam: false), Ct);
        }
        return id;
    }

    public async Task<Guid[]> StudentsAsync(int count, int specializationId, string course = Courses.Exe101)
    {
        var ids = new Guid[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = await StudentAsync(specializationId, course);
        }
        return ids;
    }

    public async Task<Guid> TeamAsync(string name, Guid leader, IEnumerable<Guid>? members = null, string course = Courses.Exe101,
        IReadOnlyList<int>? domainIds = null, string status = TeamStatuses.Open)
    {
        var teamId = Guid.NewGuid();
        await Projector.HandleAsync(new TeamCreated(teamId, SemesterId, course, name, leader, null), Ct);
        foreach (var member in members ?? [])
        {
            await Projector.HandleAsync(new MemberJoined(teamId, member), Ct);
        }
        if (domainIds is not null)
        {
            await Projector.HandleAsync(new ProjectCreated(Guid.NewGuid(), teamId, domainIds), Ct);
        }
        if (status == TeamStatuses.NeedsReview)
        {
            await Projector.HandleAsync(new TeamNeedsReview(teamId), Ct);
        }
        else if (status == TeamStatuses.Finalized)
        {
            await Projector.HandleAsync(new TeamFinalized(teamId), Ct);
        }
        return teamId;
    }

    public Task LecturerAsync(Guid lecturerId, params int[] domainIds) =>
        Projector.HandleAsync(new LecturerRegistered(lecturerId, domainIds, true), Ct);

    private static string SkillCode(Skill skill) => skill switch
    {
        Skill.Technology => "TECHNOLOGY",
        Skill.UiUxDesign => "UI_UX_DESIGN",
        Skill.Management => "MANAGEMENT",
        Skill.Marketing => "MARKETING",
        _ => "COMMUNICATION_PLANNING",
    };
}
