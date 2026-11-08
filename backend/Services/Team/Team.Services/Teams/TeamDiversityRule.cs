using Team.Repositories.BaselineImport;
using Team.Repositories.Members;

namespace Team.Services.Teams;

/// <summary>
/// Quy tắc đa dạng ngành hoặc chuyên ngành (rule 2, BE-91/BE-92):
/// Nhóm 4–6 thành viên, ít nhất 2 ngành hoặc chuyên ngành khác nhau.
/// Khóa = (MajorId, SpecializationId ?? "ngành")
/// - 2 SV khác ngành -> khác (tính là đa dạng).
/// - 2 SV cùng ngành nhưng khác chuyên ngành đã khai -> khác (tính là đa dạng).
/// - 2 SV cùng ngành và chưa khai chuyên ngành -> coi như giống nhau.
/// </summary>
public static class TeamDiversityRule
{
    public static int CountDiversity(IEnumerable<(int MajorId, int? SpecializationId)> members) =>
        members
            .Select(m => (m.MajorId, m.SpecializationId is { } specId && specId > 0 ? specId : (int?)null))
            .Distinct()
            .Count();

    public static int CountDiversity(IEnumerable<TeamMember> members) =>
        CountDiversity(members.Select(m => (m.MajorId, (int?)m.SpecializationId)));

    public static int CountDiversity(IEnumerable<TeamBaselineMember> members) =>
        CountDiversity(members.Select(m => (m.MajorId, (int?)m.SpecializationId)));
}
