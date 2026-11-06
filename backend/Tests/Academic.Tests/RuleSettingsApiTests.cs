using System.Net;
using System.Net.Http.Json;
using Academic.Repositories.Settings;
using Academic.Services.Caching;
using Academic.Services.Settings;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Tests;

/// <summary>
/// API cấu hình quy tắc (BE-23) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>). Trả lại dòng cấu hình như cũ khi xong.
/// </summary>
public sealed class RuleSettingsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private RuleSetting? _original;
    private HttpClient Admin => _api.ClientAs(Roles.Admin);

    [Fact]
    public async Task Admin_updates_rules_and_everyone_reads_fresh_values()
    {
        if (!Enabled)
        {
            return;
        }

        var student = _api.ClientAs(Roles.Student);
        var before = (await student.GetFromJsonAsync<RuleSettingsResponse>("/api/rule-settings"))!; // đã nạp cache

        var response = await Admin.PutAsJsonAsync("/api/rule-settings", new { invitationExpiryHours = before.InvitationExpiryHours + 1, reminderHours = new[] { 12, 48 } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<RuleSettingsResponse>())!;
        Assert.Equal((before.InvitationExpiryHours + 1, before.MaxTeamSize), (updated.InvitationExpiryHours, updated.MaxTeamSize));
        Assert.Equal([48, 12], updated.ReminderHours);

        var fresh = (await student.GetFromJsonAsync<RuleSettingsResponse>("/api/rule-settings"))!; // cache đã xóa
        Assert.Equal(before.InvitationExpiryHours + 1, fresh.InvitationExpiryHours);
        Assert.Equal([48, 12], fresh.ReminderHours);
        Assert.NotNull(await _api.QueryDbAsync(db => db.RuleSettings.Where(s => s.Id == 1).Select(s => s.UpdatedBy).SingleAsync()));
    }

    [Fact]
    public async Task Only_admin_can_update_and_invalid_rules_are_rejected()
    {
        if (!Enabled)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientAs(Roles.Student).PutAsJsonAsync("/api/rule-settings", new { maxTeamSize = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/rule-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/rule-settings", new { minTeamSize = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PutAsJsonAsync("/api/rule-settings", new { minTeamSize = 7, maxTeamSize = 6 })).StatusCode);
    }

    public async Task InitializeAsync()
    {
        if (Enabled)
        {
            _original = await _api.QueryDbAsync(db => db.RuleSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1));
        }
    }

    public async Task DisposeAsync()
    {
        if (Enabled && _original is { } o)
        {
            await _api.QueryDbAsync(db => db.RuleSettings.Where(s => s.Id == 1).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.MinTeamSize, o.MinTeamSize).SetProperty(x => x.MaxTeamSize, o.MaxTeamSize)
                .SetProperty(x => x.MinSpecializations, o.MinSpecializations).SetProperty(x => x.InvitationExpiryHours, o.InvitationExpiryHours)
                .SetProperty(x => x.ReminderHours, o.ReminderHours).SetProperty(x => x.UpdatedBy, o.UpdatedBy)));
            await _api.Services.GetRequiredService<AcademicCache>().InvalidateAsync(AcademicCacheKeys.RuleSettings, CancellationToken.None);
        }
        await _api.DisposeAsync();
    }
}
