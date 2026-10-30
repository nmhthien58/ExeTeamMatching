using Academic.Services.Settings;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Settings;

/// <summary>Cấu hình quy tắc: W05 (BE-23) – academic.yaml tag Settings.</summary>
[ApiController]
[Authorize]
[Route("api/rule-settings")]
public sealed class RuleSettingsController(IRuleSettingService settings) : ControllerBase
{
    [HttpGet]
    public Task<RuleSettingsResponse> Get(CancellationToken ct) => settings.GetAsync(ct);

    /// <summary>Sửa cấu hình; trường bỏ trống thì giữ nguyên.</summary>
    [HttpPut]
    [Authorize(Policy = Policies.Admin)]
    public Task<RuleSettingsResponse> Update(RuleSettingsInput input, CancellationToken ct) => settings.UpdateAsync(input, User, ct);
}
