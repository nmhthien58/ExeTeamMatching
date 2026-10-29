using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.LecturerPreferences;

namespace Team.Api.Controllers.LecturerPreferences;

/// <summary>Nguyện vọng giảng viên 1–3 của nhóm (rule 9, S23, S24, L07) – team.yaml tag Lecturers.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class LecturerPreferencesController(ILecturerPreferencesService preferences) : ControllerBase
{
    /// <summary>Nguyện vọng 1–3 của nhóm kèm giảng viên quan tâm và giảng viên EXE101 cùng đề tài (S23, S25).</summary>
    [HttpGet("teams/{teamId:guid}/lecturer-preferences")]
    public async Task<ActionResult<LecturerPreferenceListResponse>> GetPreferences(Guid teamId, CancellationToken ct) =>
        Ok(await preferences.GetPreferencesAsync(teamId, User, ct));

    /// <summary>Trưởng nhóm cập nhật cả danh sách nguyện vọng (thứ tự trong mảng = thứ hạng rank 1–3).</summary>
    [HttpPut("teams/{teamId:guid}/lecturer-preferences")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<LecturerPreferenceListResponse>> UpdatePreferences(
        Guid teamId,
        [FromBody] UpdateLecturerPreferencesInput input,
        CancellationToken ct) =>
        Ok(await preferences.UpdatePreferencesAsync(teamId, input, User, ct));

    /// <summary>Nhóm chọn tôi làm nguyện vọng (L07 tab "Nhóm chọn tôi", L01) – chỉ xem.</summary>
    [HttpGet("lecturer-preferences/me")]
    [Authorize(Policy = Policies.Lecturer)]
    public async Task<ActionResult<IReadOnlyList<LecturerPreferenceMeItem>>> GetMyPreferences(
        [FromQuery] string? courseCode,
        CancellationToken ct) =>
        Ok(await preferences.GetPreferencesMeAsync(courseCode, User, ct));
}
