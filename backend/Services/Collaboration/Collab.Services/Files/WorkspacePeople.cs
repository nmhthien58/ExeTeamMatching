using Collab.Repositories.Workspaces;

namespace Collab.Services.Files;

/// <summary>Schema <c>PersonRef</c> trong collab.yaml – người kèm vai trò trong workspace (RoleChip).</summary>
public sealed record PersonRef(string PersonType, Guid PersonId, string DisplayName, string? Role);

/// <summary>Danh bạ người của một workspace (cả người đã rời) để hiện tên và vai trò trong lịch sử việc, file, bình luận.</summary>
public sealed class WorkspacePeople(IEnumerable<WorkspaceMember> members)
{
    // Khóa chính của workspace_members là (workspace, person_type, person_id) nên mỗi người một dòng.
    private readonly Dictionary<(string, Guid), WorkspaceMember> _byPerson = members.ToDictionary(m => (m.PersonType, m.PersonId));

    public PersonRef Ref(string personType, Guid personId, string? fallbackName = null) =>
        _byPerson.TryGetValue((personType, personId), out var member)
            ? new PersonRef(personType, personId, member.DisplayName, member.Role)
            : new PersonRef(personType, personId, fallbackName ?? "Người dùng", null);

    public PersonRef Student(Guid studentId) => Ref(PersonTypes.Student, studentId);

    public string NameOf(string personType, Guid personId) => Ref(personType, personId).DisplayName;

    /// <summary>Sinh viên đang ở nhóm (để kiểm tra người làm, gửi sự kiện cho cả nhóm).</summary>
    public IReadOnlyList<Guid> ActiveStudentIds => _byPerson.Values
        .Where(m => m.PersonType == PersonTypes.Student && m.LeftAt is null)
        .Select(m => m.PersonId).ToList();

    /// <summary>Giảng viên hướng dẫn đang hiệu lực, nếu có.</summary>
    public Guid? ActiveInstructorId => _byPerson.Values
        .FirstOrDefault(m => m.Role == WorkspaceRoles.Instructor && m.LeftAt is null)?.PersonId;
}
