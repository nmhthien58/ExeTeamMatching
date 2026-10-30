using Academic.Repositories.Settings;
using Academic.Services.Settings;
using BuildingBlocks.Errors;

namespace Academic.Tests;

/// <summary>Kiểm tra cấu hình quy tắc (BE-23), không cần DB.</summary>
public sealed class RuleSettingRulesTests
{
    private static RuleSettingsInput Input(int? min = null, int? max = null, int? specializations = null, IReadOnlyList<int>? reminders = null) =>
        new(min, max, specializations, null, reminders, null, null, null, null, null);

    [Fact]
    public void Blank_fields_keep_current_values()
    {
        var setting = new RuleSetting { InvitationExpiryHours = 48, MaxFileSizeMb = 30 };
        RuleSettingRules.Apply(setting, Input(max: 7));
        Assert.Equal((4, 7, 2, 48, 30), ((int)setting.MinTeamSize, (int)setting.MaxTeamSize, (int)setting.MinSpecializations, (int)setting.InvitationExpiryHours, (int)setting.MaxFileSizeMb));
        Assert.Equal(new short[] { 72, 24 }, setting.ReminderHours);
    }

    [Fact]
    public void Reminder_hours_are_sorted_descending_and_may_be_empty()
    {
        var setting = new RuleSetting();
        RuleSettingRules.Apply(setting, Input(reminders: [12, 48, 24]));
        Assert.Equal(new short[] { 48, 24, 12 }, setting.ReminderHours);
        RuleSettingRules.Apply(setting, Input(reminders: []));
        Assert.Empty(setting.ReminderHours);
    }

    [Theory]
    [InlineData(7, null, null, "maxTeamSize")] // tối thiểu mới > tối đa đang lưu
    [InlineData(null, 3, null, "maxTeamSize")]
    [InlineData(null, null, 7, "minSpecializations")]
    public void Relations_are_checked_after_merging(int? min, int? max, int? specializations, string field)
    {
        var setting = new RuleSetting();
        var error = Assert.Throws<RequestValidationException>(() => RuleSettingRules.Apply(setting, Input(min, max, specializations)));
        Assert.True(error.Errors.ContainsKey(field));
        Assert.Equal(4, setting.MinTeamSize); // lỗi thì không đổi gì
    }

    [Theory]
    [InlineData(1, null, null)]
    [InlineData(null, 51, null)]
    [InlineData(null, null, 0)]
    public void Team_rules_must_be_in_range(int? min, int? max, int? specializations) =>
        Assert.False(new RuleSettingsInputValidator().Validate(Input(min, max, specializations)).IsValid);

    [Fact]
    public void Each_field_is_limited_like_the_database()
    {
        var validator = new RuleSettingsInputValidator();
        Assert.True(validator.Validate(Input()).IsValid); // trường bỏ trống không bị kiểm tra
        Assert.False(validator.Validate(Input() with { InvitationExpiryHours = 0 }).IsValid);
        Assert.False(validator.Validate(Input() with { MaxIdeasPerJoinRequest = 11 }).IsValid);
        Assert.False(validator.Validate(Input() with { InterestExtraSlots = -1 }).IsValid);
        Assert.False(validator.Validate(Input() with { MaxInterestsUnlimited = 101 }).IsValid);
        Assert.False(validator.Validate(Input() with { MaxLecturerPreferences = 0 }).IsValid);
        Assert.False(validator.Validate(Input() with { MaxFileSizeMb = 101 }).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(721)]
    public void Reminder_hour_must_be_in_range(int hour) =>
        Assert.False(new RuleSettingsInputValidator().Validate(Input(reminders: [hour])).IsValid);

    [Fact]
    public void Reminders_must_be_distinct_and_at_most_five()
    {
        var validator = new RuleSettingsInputValidator();
        Assert.False(validator.Validate(Input(reminders: [24, 24])).IsValid);
        Assert.False(validator.Validate(Input(reminders: [1, 2, 3, 4, 5, 6])).IsValid);
        Assert.True(validator.Validate(Input(reminders: [1, 2, 3, 4, 5])).IsValid);
    }
}
