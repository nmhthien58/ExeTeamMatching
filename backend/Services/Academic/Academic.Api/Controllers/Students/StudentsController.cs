using Academic.Services.Students;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Students;

/// <summary>Hồ sơ sinh viên: A05, S02, S03 (BE-19, BE-67) – academic.yaml tag Students. Danh sách, chi tiết (W09, W10) ở BE-18.</summary>
[ApiController]
[Authorize]
[Route("api/students")]
public sealed class StudentsController(IStudentProfileService profiles) : ControllerBase
{
    /// <summary>Hồ sơ của tôi; dữ liệu import (họ tên, MSSV, chuyên ngành, lớp, SĐT) chỉ đọc.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.Student)]
    public Task<StudentProfileResponse> Me(CancellationToken ct) => profiles.GetMeAsync(User, ct);

    /// <summary>Sửa 5 kỹ năng, mô tả, công tắc tìm nhóm; phát StudentProfileUpdated.</summary>
    [HttpPut("me")]
    [Authorize(Policy = Policies.Student)]
    public Task<StudentProfileResponse> UpdateMe(StudentProfileInput input, CancellationToken ct) => profiles.UpdateMeAsync(input, User, ct);
}
