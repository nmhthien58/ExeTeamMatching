// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Settings;

/// <summary>Bảng <c>academic.rule_settings</c> – cấu hình quy tắc (W05). Chỉ có một dòng, <see cref="Id"/> = 1.</summary>
public class RuleSetting
{
    public const short SingletonId = 1;

    public short Id { get; set; } = SingletonId;
    public short MinTeamSize { get; set; } = 4;
    public short MaxTeamSize { get; set; } = 6;
    public short MinSpecializations { get; set; } = 2;
    public short InvitationExpiryHours { get; set; } = 72;
    public short[] ReminderHours { get; set; } = [72, 24];

    /// <summary>Rule 17.</summary>
    public short MaxIdeasPerJoinRequest { get; set; } = 3;

    /// <summary>Rule 19: giới hạn quan tâm = số nhóm còn nhận + n (mentor: tối đa + n).</summary>
    public short InterestExtraSlots { get; set; } = 2;

    /// <summary>Rule 19: giới hạn quan tâm khi giảng viên không có số nhóm tối đa.</summary>
    public short MaxInterestsUnlimited { get; set; } = 10;

    /// <summary>Rule 9: số nguyện vọng giảng viên của nhóm.</summary>
    public short MaxLecturerPreferences { get; set; } = 3;

    /// <summary>Rule 24.</summary>
    public short MaxFileSizeMb { get; set; } = 25;

    /// <summary>BE-89: số nhóm tối đa của môn trong kỳ = floor(số sinh viên của môn × hệ số).</summary>
    public decimal MaxTeamRatio { get; set; } = 0.20m;

    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
