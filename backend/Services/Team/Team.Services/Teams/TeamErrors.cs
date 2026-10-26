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

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
