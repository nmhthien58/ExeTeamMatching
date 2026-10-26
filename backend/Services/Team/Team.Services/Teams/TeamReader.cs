using Team.Repositories.Teams;
using Team.Services.Academic;

namespace Team.Services.Teams;

/// <summary>Dựng TeamCard / StudentRef cho các module (lời mời, yêu cầu tham gia…). Academic lỗi chỉ làm mất tên danh mục / sinh viên.</summary>
public sealed class TeamReader(ITeamRepository teams, IAcademicClient academic)
{
    public async Task<Catalogs> LoadCatalogsAsync(CancellationToken ct)
    {
        try
        {
            return new Catalogs((await academic.GetDomainsAsync(ct)).ToDictionary(d => d.Id),
                (await academic.GetSpecializationsAsync(ct)).ToDictionary(s => s.Id));
        }
        catch (HttpRequestException)
        {
            return Catalogs.Empty;
        }
    }

    /// <summary>
    /// Thẻ nhóm và StudentRef cho các cặp (nhóm, sinh viên) của lời mời / yêu cầu tham gia.
    /// Team không lưu tên người được mời / người xin vào nên tra Academic theo học kỳ của nhóm.
    /// </summary>
    public async Task<(Dictionary<Guid, TeamCardResponse> Cards, Dictionary<Guid, StudentRef> Students)> CardsAndStudentsAsync(
        IReadOnlyCollection<(Guid TeamId, Guid StudentId)> pairs, CancellationToken ct)
    {
        var catalogs = await LoadCatalogsAsync(ct);
        var graphs = await teams.ListGraphsAsync(pairs.Select(p => p.TeamId).Distinct().ToList(), ct);
        var semesterOf = graphs.ToDictionary(g => g.Team.Id, g => g.Team.SemesterId);
        var students = new Dictionary<Guid, StudentRef>();
        foreach (var bySemester in pairs.Where(p => semesterOf.ContainsKey(p.TeamId)).GroupBy(p => semesterOf[p.TeamId]))
        {
            foreach (var (id, student) in await StudentsAsync(bySemester.Key, bySemester.Select(p => p.StudentId), catalogs, ct))
            {
                students[id] = student;
            }
        }
        return (graphs.ToDictionary(g => g.Team.Id, g => TeamMapper.ToCard(g, catalogs)), students);
    }

    private async Task<Dictionary<Guid, StudentRef>> StudentsAsync(Guid semesterId, IEnumerable<Guid> studentIds, Catalogs catalogs, CancellationToken ct)
    {
        var ids = studentIds.Distinct().ToList();
        IReadOnlyList<StudentSnapshot> found;
        try
        {
            found = ids.Count == 0 ? [] : await academic.LookupStudentsAsync(semesterId, ids, ct);
        }
        catch (HttpRequestException)
        {
            found = [];
        }
        var byId = found.ToDictionary(s => s.Id);
        return ids.ToDictionary(id => id, id => byId.TryGetValue(id, out var s)
            ? new StudentRef(id, s.StudentCode, s.FullName, catalogs.Specialization(s.SpecializationId))
            : new StudentRef(id, "", "", new CatalogRef(0, null, null)));
    }
}
