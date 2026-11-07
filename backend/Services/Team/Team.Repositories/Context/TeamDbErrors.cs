using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Team.Repositories.Context;

/// <summary>
/// Đổi lỗi ràng buộc của team_db (uq_*, ck_*, fk_*, trigger) thành <see cref="AppException"/> để trả ProblemDetails 409
/// với mã trong docs/contracts/README.md. Services vẫn kiểm tra trước; đây là lưới chặn khi hai request chạy cùng lúc.
/// </summary>
public static class TeamDbErrors
{
    public static AppException? ToAppException(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => new ConflictException("concurrency-conflict", "Dữ liệu vừa được người khác thay đổi", "Tải lại rồi thử lại."),
        // Trigger DEFERRABLE báo lỗi lúc COMMIT nên có thể là PostgresException trực tiếp, không bọc trong DbUpdateException.
        _ when (exception as PostgresException ?? exception.InnerException as PostgresException) is { } pg => FromPostgres(pg),
        _ => null,
    };

    private static AppException? FromPostgres(PostgresException pg) => pg.ConstraintName switch
    {
        "uq_team_members_semester_student" => new ConflictException("already-in-team", "Sinh viên đã có nhóm", "Sinh viên đã thuộc một nhóm trong học kỳ này."),
        "ck_teams_member_count" => new ConflictException("team-full", "Nhóm đã đủ người", "Nhóm đã đủ sĩ số tối đa."),
        "uq_invitations_pending" or "uq_join_requests_pending" or "uq_change_requests_pending" =>
            new ConflictException("pending-exists", "Đã có yêu cầu đang chờ", "Đã có lời mời hoặc yêu cầu đang chờ xử lý."),
        "uq_leader_votes_one_open" => new ConflictException("leader-vote-open", "Nhóm đang có đề nghị", "Nhóm đang có một đề nghị đổi nhóm trưởng chưa kết thúc."),
        "ck_team_finalized_size" or "ck_team_finalized_specializations" =>
            new ConflictException("composition-invalid", "Nhóm chưa đủ điều kiện", "Nhóm chưa đủ sĩ số hoặc số chuyên ngành theo quy tắc."),
        "ck_join_requests_ideas" => new ConflictException("ideas-required", "Thiếu ý tưởng", "Cần 1–3 ý tưởng hoặc chọn \"Chưa có ý tưởng\"."),
        "uq_lecturer_assignments_active" or "uq_mentor_assignments_active" =>
            new ConflictException("team-has-advisor", "Nhóm đã có cố vấn", "Nhóm đã có cố vấn cùng loại đang hiệu lực."),
        _ when pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation =>
            new ConflictException("constraint-violation", "Dữ liệu không hợp lệ", $"Vi phạm ràng buộc {pg.ConstraintName}."),
        _ => null,
    };
}
