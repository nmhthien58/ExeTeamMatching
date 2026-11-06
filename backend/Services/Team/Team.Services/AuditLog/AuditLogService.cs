using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Auth;
using Team.Repositories.AuditLog;
using Team.Repositories.Teams;
using Team.Services.Teams;

namespace Team.Services.AuditLog;

public interface IAuditLogService
{
    /// <summary>S22, L05 – lịch sử nhóm cho thành viên nhóm, giảng viên hướng dẫn / mentor của nhóm và Admin.</summary>
    Task<PagedResponse<AuditLogResponse>> ListForTeamAsync(Guid teamId, AuditLogPageQuery query, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>W24 – nhật ký hệ thống cho Admin.</summary>
    Task<PagedResponse<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken ct);
}

/// <summary>Module AuditLog của Team Service (BE-32, rule 10) – chỉ đọc; ghi bằng <see cref="AuditLogWriter"/>.</summary>
public sealed class AuditLogService(IAuditLogRepository logs, ITeamRepository teams) : IAuditLogService
{
    public async Task<PagedResponse<AuditLogResponse>> ListForTeamAsync(Guid teamId, AuditLogPageQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var studentId = user.GetStudentId();
        var allowed = user.IsInRole(Roles.Admin)
            || graph.Members.Any(m => m.StudentId == studentId)
            || await logs.IsAdvisorAsync(teamId, user.GetLecturerId(), user.GetMentorId(), ct);
        if (!allowed)
        {
            throw TeamErrors.NotMember();
        }
        return await PageAsync(new AuditLogFilter(teamId, null, null, null, query.Page, query.PageSize), ct);
    }

    public Task<PagedResponse<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken ct) =>
        PageAsync(new AuditLogFilter(query.TeamId, query.ActorAccountId, query.From, query.To, query.Page, query.PageSize, query.ActorName), ct);

    private async Task<PagedResponse<AuditLogResponse>> PageAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var (items, total) = await logs.SearchAsync(filter, ct);
        return new PagedResponse<AuditLogResponse>(items.Select(ToResponse).ToList(), filter.Page, filter.PageSize, total,
            (total + filter.PageSize - 1) / filter.PageSize);
    }

    private static AuditLogResponse ToResponse(AuditLogEntry e) =>
        new(e.Id, e.TeamId, e.Action, e.ActorName ?? ActorName(e.ActorRole), e.ActorRole, Summary(e), e.Reason, e.OccurredAt);

    /// <summary>Log của hệ thống và log trước khi có cột <c>actor_name</c> hiện theo vai trò.</summary>
    private static string ActorName(string role) => role switch
    {
        Roles.Admin => "Bộ môn",
        Roles.Lecturer => "Giảng viên",
        Roles.Mentor => "Mentor",
        Roles.Student => "Sinh viên",
        _ => "Hệ thống",
    };

    /// <summary>Câu mô tả tiếng Việt từ mã hành động và giá trị đã lưu; hành động của module khác hiện nguyên mã.</summary>
    public static string Summary(AuditLogEntry e)
    {
        string? New(string property) => Read(e.NewValues, property);
        string? Old(string property) => Read(e.OldValues, property);
        return e.Action switch
        {
            "TEAM_CREATED" => $"Tạo nhóm {New("name")}",
            "TEAM_UPDATED" => "Sửa thông tin nhóm",
            "INVITATION_SENT" => $"Mời {New("studentCode")} vào nhóm",
            "INVITATION_CANCELLED" => "Thu hồi lời mời",
            "JOIN_REQUEST_SUBMITTED" => $"{New("studentCode")} xin vào nhóm",
            "JOIN_REQUEST_CANCELLED" => "Hủy yêu cầu tham gia",
            "MEMBER_JOINED" => $"{New("studentCode")} vào nhóm",
            "MEMBER_ADD_PENDING" => $"{New("studentCode")} vào nhóm – chờ Bộ môn duyệt",
            "MEMBER_LEFT" => $"{Old("studentCode")} rời nhóm",
            "MEMBER_REMOVED" => $"Xóa {Old("studentCode")} khỏi nhóm",
            "MEMBER_REMOVE_PENDING" => $"{New("studentCode")} rời nhóm – chờ Bộ môn duyệt",
            "LEADER_CHANGED" => $"Chuyển trưởng nhóm từ {Old("leader")} sang {New("leader")}",
            "RECRUITMENT_POST_OPENED" => "Đăng bài tuyển thành viên",
            "RECRUITMENT_POST_UPDATED" => "Sửa bài tuyển thành viên",
            "RECRUITMENT_POST_CLOSED" => "Đóng bài tuyển thành viên",
            "LECTURER_ASSIGNED" => LecturerAssigned(Old("lecturerName"), New("lecturerName"), New("source")),
            "TOPIC_INTEREST_OPENED" => New("advisorName") is { } advisor ? $"{advisor} quan tâm đề tài của nhóm" : "Cố vấn quan tâm đề tài của nhóm",
            "TOPIC_INTEREST_WITHDRAWN" => "Rút quan tâm đề tài",
            "MEMBER_ADDED" => $"Bộ môn thêm {New("studentCode")} vào nhóm",
            "TEAM_FINALIZED" => "Chốt nhóm",
            "TEAM_NEEDS_REVIEW" => "Nhóm cần Bộ môn xem lại",
            "TEAM_REOPENED" => "Mở lại nhóm",
            "TEAM_DISBANDED" => "Giải tán nhóm",
            "TEAM_LINKED_TO_PROJECT" => "Gắn nhóm với đề tài của kỳ trước",
            "CHANGE_REQUEST_SUBMITTED" => $"Gửi yêu cầu đổi nhóm ({New("changedMemberCount")} thay đổi)",
            "CHANGE_REQUEST_APPROVED" => "Bộ môn duyệt yêu cầu đổi nhóm",
            "CHANGE_REQUEST_REJECTED" => "Bộ môn từ chối yêu cầu đổi nhóm",
            "PROJECT_CHANGE_PENDING" => $"Đề xuất đổi đề tài thành {New("newProjectName")} – chờ Bộ môn duyệt",
            "PROJECT_CHANGE_CANCELLED" => "Hủy đề xuất đổi đề tài",
            "PROJECT_PIVOT_APPROVED" => $"Đổi đề tài thành {New("name")}",
            "LECTURER_PREFERENCES_UPDATED" => "Sửa nguyện vọng giảng viên",
            "MENTOR_ASSIGNED" => MentorAssigned(Old("mentorName"), New("mentorName"), New("source")),
            "MENTOR_UNASSIGNED" => $"Gỡ mentor {Old("mentorName")}",
            "UNMATCHED_REMINDED" => $"Nhắc {New("studentCount")} sinh viên chưa có nhóm",
            _ => e.Action,
        };
    }

    /// <summary><c>LECTURER_ASSIGNED</c> do <c>LecturerAssignmentWriter</c> ghi: <c>lecturerName</c>, <c>source</c> AUTO / MANUAL; old_values có khi đổi giảng viên.</summary>
    private static string LecturerAssigned(string? oldName, string? newName, string? source)
    {
        var name = string.IsNullOrWhiteSpace(newName) ? "giảng viên" : newName;
        if (!string.IsNullOrWhiteSpace(oldName))
        {
            return $"Đổi giảng viên hướng dẫn từ {oldName} sang {name}";
        }
        return source == "AUTO" ? $"Hệ thống ghép giảng viên hướng dẫn {name}" : $"Gán giảng viên hướng dẫn {name}";
    }

    /// <summary><c>MENTOR_ASSIGNED</c> do <c>MentorAssignmentWriter</c> ghi: <c>mentorName</c>, <c>source</c> AUTO / MANUAL; old_values có khi đổi mentor.</summary>
    private static string MentorAssigned(string? oldName, string? newName, string? source)
    {
        var name = string.IsNullOrWhiteSpace(newName) ? "mentor" : newName;
        if (!string.IsNullOrWhiteSpace(oldName))
        {
            return $"Đổi mentor từ {oldName} sang {name}";
        }
        return source == "AUTO" ? $"Hệ thống ghép mentor {name}" : $"Gán mentor {name}";
    }

    private static string? Read(string? json, string property)
    {
        if (json is null)
        {
            return null;
        }
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(property, out var value)
            ? value.ToString()
            : null;
    }
}
