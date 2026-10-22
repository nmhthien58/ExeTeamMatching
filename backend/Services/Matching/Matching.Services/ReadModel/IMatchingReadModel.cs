namespace Matching.Services.ReadModel;

/// <summary>
/// Read model của Matching (PROJECT_CONTEXT §5.5) – chỉ dựng từ sự kiện Kafka, không có SQL.
/// Mỗi thao tác ghi chỉ đụng tới các trường của nó nên hai consumer (exe.student, exe.team) chạy song song
/// không ghi đè lẫn nhau; ghi lại cùng giá trị không đổi kết quả (idempotent).
/// </summary>
public interface IMatchingReadModel
{
    Task<StudentView?> GetStudentAsync(Guid studentId, CancellationToken ct);

    /// <summary>Sinh viên của một học kỳ và môn.</summary>
    Task<IReadOnlyList<StudentView>> GetStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct);

    Task<IReadOnlyList<StudentView>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken ct);

    Task<TeamView?> GetTeamAsync(Guid teamId, CancellationToken ct);

    /// <summary>Nhóm của một học kỳ; <paramref name="courseCode"/> null = cả hai môn.</summary>
    Task<IReadOnlyList<TeamView>> GetTeamsAsync(Guid semesterId, string? courseCode, CancellationToken ct);

    Task<AdvisorView?> GetAdvisorAsync(AdvisorKind kind, Guid advisorId, CancellationToken ct);

    // ---------- Sinh viên ----------

    /// <summary>Sinh viên được import vào học kỳ (StudentImported).</summary>
    Task SaveStudentAsync(Guid studentId, Guid semesterId, string courseCode, string? fullName, int specializationId, CancellationToken ct);

    /// <summary>Hồ sơ: chuyên ngành, kỹ năng 1–5, cờ đang tìm nhóm (StudentProfileUpdated).</summary>
    Task SaveStudentProfileAsync(Guid studentId, string? fullName, int specializationId, IReadOnlyDictionary<Skill, int> skills,
        bool isSeekingTeam, CancellationToken ct);

    /// <summary>
    /// Vào nhóm (<paramref name="isMember"/> = true) hoặc rời nhóm. Rời chỉ xóa khi sinh viên đang ở đúng nhóm đó,
    /// để sự kiện tới lệch thứ tự không xóa nhầm nhóm mới.
    /// </summary>
    Task SetStudentTeamAsync(Guid studentId, Guid teamId, bool isMember, CancellationToken ct);

    // ---------- Nhóm ----------

    Task SaveTeamAsync(Guid teamId, Guid semesterId, string courseCode, string name, Guid? projectId, CancellationToken ct);

    Task AddTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct);

    Task RemoveTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct);

    Task SetTeamStatusAsync(Guid teamId, string status, CancellationToken ct);

    /// <summary>Gắn nhóm với đề tài; domain của nhóm lấy theo đề tài.</summary>
    Task LinkTeamToProjectAsync(Guid teamId, Guid projectId, CancellationToken ct);

    /// <summary>Domain của đề tài (ProjectCreated); cập nhật luôn các nhóm đã gắn đề tài.</summary>
    Task SaveProjectDomainsAsync(Guid projectId, IReadOnlyList<int> domainIds, CancellationToken ct);

    /// <summary>Giảng viên hướng dẫn đề tài ở EXE101 – ưu tiên trong gợi ý đề tài EXE201 (rule 26).</summary>
    Task SetProjectExe101LecturerAsync(Guid projectId, Guid lecturerId, CancellationToken ct);

    /// <summary>Cố vấn hiện tại của nhóm (LecturerAssigned / MentorAssigned / MentorUnassigned); null = không có.</summary>
    Task SetTeamAdvisorAsync(Guid teamId, AdvisorKind kind, Guid? advisorId, CancellationToken ct);

    Task SetInterestAsync(Guid teamId, AdvisorKind kind, Guid advisorId, bool isOpen, CancellationToken ct);

    // ---------- Cố vấn ----------

    /// <summary>
    /// Giảng viên / mentor (LecturerRegistered, LecturerProfileUpdated, MentorRegistered).
    /// <paramref name="domainIds"/> null = giữ domain đã lưu (payload không có trường này).
    /// </summary>
    Task SaveAdvisorAsync(AdvisorKind kind, Guid advisorId, IReadOnlyList<int>? domainIds, bool isActive, CancellationToken ct);
}
