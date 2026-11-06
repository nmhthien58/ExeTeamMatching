using System.Security.Claims;
using Academic.Repositories.Settings;
using Academic.Services.Caching;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services.Settings;

public interface IRuleSettingService
{
    /// <summary>Cấu hình hiện tại (cache Redis). DB chưa seed thì trả mặc định.</summary>
    Task<RuleSettingsResponse> GetAsync(CancellationToken ct);

    Task<RuleSettingsResponse> UpdateAsync(RuleSettingsInput input, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Cấu hình quy tắc (BE-23, W05, rule 8): một dòng duy nhất trong <c>rule_settings</c>. Mọi người đã đăng nhập và các service đọc qua
/// <c>GET /api/rule-settings</c>, Admin sửa. Hạn chọn slot, độ dài slot, tuần Checkpoint thuộc từng học kỳ (BE-16).
/// </summary>
public sealed class RuleSettingService(IRuleSettingRepository settings, AcademicCache cache) : IRuleSettingService
{
    public Task<RuleSettingsResponse> GetAsync(CancellationToken ct) =>
        cache.GetOrLoadAsync(AcademicCacheKeys.RuleSettings, async token =>
            RuleSettingsResponse.From(await settings.GetAsync(token) ?? new RuleSetting()), ct);

    public async Task<RuleSettingsResponse> UpdateAsync(RuleSettingsInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var setting = await settings.FindAsync(ct);
        if (setting is null)
        {
            setting = new RuleSetting();
            settings.Add(setting);
        }
        RuleSettingRules.Apply(setting, input);
        setting.UpdatedBy = user.GetUserId();
        await settings.SaveAsync(ct);
        await cache.InvalidateAsync(AcademicCacheKeys.RuleSettings, ct);
        return RuleSettingsResponse.From(setting);
    }
}

public static class RuleSettingRules
{
    /// <summary>Ghép dữ liệu mới vào cấu hình rồi kiểm tra quan hệ giữa các trường trên giá trị sau khi ghép. Mốc nhắc sắp giảm dần.</summary>
    public static void Apply(RuleSetting s, RuleSettingsInput input)
    {
        var min = input.MinTeamSize ?? s.MinTeamSize;
        var max = input.MaxTeamSize ?? s.MaxTeamSize;
        var specializations = input.MinSpecializations ?? s.MinSpecializations;
        var errors = new Dictionary<string, string[]>();
        if (max < min)
        {
            errors["maxTeamSize"] = ["Sĩ số tối đa phải lớn hơn hoặc bằng sĩ số tối thiểu."];
        }
        if (specializations > max)
        {
            errors["minSpecializations"] = ["Số chuyên ngành tối thiểu không được vượt sĩ số tối đa."];
        }
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        s.MinTeamSize = (short)min;
        s.MaxTeamSize = (short)max;
        s.MinSpecializations = (short)specializations;
        s.InvitationExpiryHours = (short)(input.InvitationExpiryHours ?? s.InvitationExpiryHours);
        s.ReminderHours = input.ReminderHours?.OrderDescending().Select(h => (short)h).ToArray() ?? s.ReminderHours;
        s.MaxIdeasPerJoinRequest = (short)(input.MaxIdeasPerJoinRequest ?? s.MaxIdeasPerJoinRequest);
        s.InterestExtraSlots = (short)(input.InterestExtraSlots ?? s.InterestExtraSlots);
        s.MaxInterestsUnlimited = (short)(input.MaxInterestsUnlimited ?? s.MaxInterestsUnlimited);
        s.MaxLecturerPreferences = (short)(input.MaxLecturerPreferences ?? s.MaxLecturerPreferences);
        s.MaxFileSizeMb = (short)(input.MaxFileSizeMb ?? s.MaxFileSizeMb);
    }
}

public static class SettingsModule
{
    /// <summary>Module Settings (BE-23): cấu hình quy tắc – W05.</summary>
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddScoped<IRuleSettingRepository, RuleSettingRepository>();
        services.AddScoped<IRuleSettingService, RuleSettingService>();
        return services;
    }
}
