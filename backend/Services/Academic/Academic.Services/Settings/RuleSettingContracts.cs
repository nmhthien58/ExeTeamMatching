using Academic.Repositories.Settings;
using FluentValidation;

namespace Academic.Services.Settings;

// Schema RuleSettings của academic.yaml (tag Settings). JSON camelCase.

/// <summary>Schema <c>RuleSettings</c>.</summary>
public sealed record RuleSettingsResponse(
    int MinTeamSize, int MaxTeamSize, int MinSpecializations, int InvitationExpiryHours, IReadOnlyList<int> ReminderHours,
    int MaxIdeasPerJoinRequest, int InterestExtraSlots, int MaxInterestsUnlimited, int MaxLecturerPreferences, int MaxFileSizeMb, decimal MaxTeamRatio)
{
    public static RuleSettingsResponse From(RuleSetting s) => new(
        s.MinTeamSize, s.MaxTeamSize, s.MinSpecializations, s.InvitationExpiryHours, s.ReminderHours.Select(h => (int)h).ToList(),
        s.MaxIdeasPerJoinRequest, s.InterestExtraSlots, s.MaxInterestsUnlimited, s.MaxLecturerPreferences, s.MaxFileSizeMb, s.MaxTeamRatio);
}

/// <summary>Body của <c>PUT /api/rule-settings</c>. Trường bỏ trống thì giữ nguyên.</summary>
public sealed record RuleSettingsInput(
    int? MinTeamSize, int? MaxTeamSize, int? MinSpecializations, int? InvitationExpiryHours, IReadOnlyList<int>? ReminderHours,
    int? MaxIdeasPerJoinRequest, int? InterestExtraSlots, int? MaxInterestsUnlimited, int? MaxLecturerPreferences, int? MaxFileSizeMb,
    decimal? MaxTeamRatio = null);

/// <summary>Giới hạn từng trường, khớp CHECK của <c>academic.rule_settings</c>. Quan hệ giữa các trường kiểm tra ở <see cref="RuleSettingRules"/>.</summary>
public sealed class RuleSettingsInputValidator : AbstractValidator<RuleSettingsInput>
{
    /// <summary>Lời mời, mốc nhắc tối đa 30 ngày.</summary>
    public const int MaxHours = 720;

    public const int MaxReminders = 5;

    public RuleSettingsInputValidator()
    {
        RuleFor(x => x.MinTeamSize).InclusiveBetween(2, 50).WithMessage("Sĩ số tối thiểu từ 2 đến 50.");
        RuleFor(x => x.MaxTeamSize).InclusiveBetween(2, 50).WithMessage("Sĩ số tối đa từ 2 đến 50.");
        RuleFor(x => x.MinSpecializations).InclusiveBetween(1, 50).WithMessage("Số chuyên ngành tối thiểu từ 1 đến 50.");
        RuleFor(x => x.InvitationExpiryHours).InclusiveBetween(1, MaxHours).WithMessage($"Hạn lời mời từ 1 đến {MaxHours} giờ.");
        RuleFor(x => x.ReminderHours).Must(h => h!.Count <= MaxReminders).When(x => x.ReminderHours is not null).WithMessage($"Tối đa {MaxReminders} mốc nhắc.");
        RuleFor(x => x.ReminderHours).Must(h => h!.All(v => v is >= 1 and <= MaxHours) && h!.Distinct().Count() == h!.Count)
            .When(x => x.ReminderHours is not null).WithMessage($"Mốc nhắc từ 1 đến {MaxHours} giờ, không trùng nhau.");
        RuleFor(x => x.MaxIdeasPerJoinRequest).InclusiveBetween(1, 10).WithMessage("Số ý tưởng tối đa từ 1 đến 10.");
        RuleFor(x => x.InterestExtraSlots).InclusiveBetween(0, 10).WithMessage("Số suất quan tâm thêm từ 0 đến 10.");
        RuleFor(x => x.MaxInterestsUnlimited).InclusiveBetween(1, 100).WithMessage("Giới hạn quan tâm từ 1 đến 100.");
        RuleFor(x => x.MaxLecturerPreferences).InclusiveBetween(1, 10).WithMessage("Số nguyện vọng từ 1 đến 10.");
        RuleFor(x => x.MaxFileSizeMb).InclusiveBetween(1, 100).WithMessage("Dung lượng file từ 1 đến 100 MB.");
        RuleFor(x => x.MaxTeamRatio).InclusiveBetween(0.01m, 1m).WithMessage("Hệ số số nhóm từ 0,01 đến 1.");
    }
}
