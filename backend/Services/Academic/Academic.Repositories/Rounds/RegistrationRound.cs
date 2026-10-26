// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Rounds;

/// <summary>Bảng <c>academic.registration_rounds</c> – đợt lập nhóm / đợt đổi (rule 6).</summary>
public class RegistrationRound
{
    public Guid Id { get; set; }
    public Guid SemesterId { get; set; }

    /// <summary><c>EXE101</c> hoặc <c>EXE201</c>.</summary>
    public string CourseCode { get; set; } = string.Empty;

    /// <summary><c>TEAM_FORMATION</c> hoặc <c>CHANGE_REQUEST</c> (chỉ EXE201).</summary>
    public string RoundType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset DeadlineAt { get; set; }

    /// <summary><c>SCHEDULED</c>, <c>OPEN</c>, <c>CLOSED</c>; <see cref="ClosedAt"/> có giá trị khi và chỉ khi CLOSED.</summary>
    public string Status { get; set; } = "SCHEDULED";

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary><c>identity.user_accounts.id</c> (claim sub).</summary>
    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
