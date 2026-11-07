using BuildingBlocks.Errors;

namespace Team.Services.Teams;

/// <summary>Lỗi của module Teams (mã ở docs/contracts/README.md).</summary>
public static class TeamErrors
{
    public static NotFoundException TeamNotFound(Guid teamId) => new("nhóm", teamId);

    public static NotFoundException NoTeam() => new("nhóm của bạn");

    public static ConflictException RoundClosed() =>
        new("round-closed", "Đợt lập nhóm chưa mở hoặc đã khóa", "Chỉ thao tác được khi đợt lập nhóm của môn đang mở.");

    public static ConflictException AlreadyInTeam() =>
        new("already-in-team", "Bạn đã có nhóm", "Mỗi sinh viên chỉ thuộc một nhóm trong học kỳ.");

    public static ConflictException NotLeader() =>
        new("not-leader", "Chỉ trưởng nhóm được thao tác", "Thao tác này dành cho trưởng nhóm.");

    public static ForbiddenException NotInList() =>
        new("not-in-list", "Không có trong danh sách học kỳ", "Tài khoản không có trong danh sách sinh viên của học kỳ hiện hành.");

    public static ForbiddenException StudentsOnly() => new(title: "Chỉ sinh viên được thao tác");

    public static ForbiddenException NotMember() => new(title: "Bạn không thuộc nhóm này");

    public static NotFoundException MemberNotFound(Guid studentId) => new("thành viên", studentId);

    public static ConflictException TeamFull(int maxSize) => new("team-full", "Nhóm đã đủ người", $"Nhóm đã có {maxSize} thành viên.");

    public static ConflictException TeamLimitReached(int maxTeams) => new("team-limit-reached", "Đã đủ số nhóm của môn",
        $"Môn này đã có đủ {maxTeams} nhóm trong kỳ, không tạo thêm nhóm được. Hãy tìm nhóm còn chỗ ở Khám phá.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException RequestNotPending() =>
        new("request-not-pending", "Lời mời / yêu cầu không còn chờ", "Lời mời hoặc yêu cầu đã được xử lý, đã hủy hoặc đã hết hạn.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException LeaderMustTransfer() =>
        new("leader-must-transfer", "Trưởng nhóm chưa chuyển quyền", "Chuyển quyền trưởng nhóm cho thành viên khác trước khi rời nhóm.");

    public static HttpStatusException IdeasRequired() =>
        new(400, "ideas-required", "Thiếu ý tưởng", "Thêm 1–3 ý tưởng hoặc chọn \"Chưa có ý tưởng\".");

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
