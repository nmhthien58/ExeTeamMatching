using Academic.Services;
using Academic.Services.Students;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Students;

/// <summary>Sinh viên: W09, W10, S04, S19 (BE-18); hồ sơ của tôi A05, S02, S03 (BE-19, BE-67) – academic.yaml tag Students.</summary>
[ApiController]
[Authorize]
[Route("api/students")]
public sealed class StudentsController(IStudentProfileService profiles, IStudentDirectoryService directory) : ControllerBase
{
    /// <summary>Danh sách sinh viên học kỳ; người không phải Admin không thấy email.</summary>
    [HttpGet]
    public Task<PagedResponse<StudentSummaryResponse>> List([FromQuery] StudentQuery query, CancellationToken ct) => directory.ListAsync(query, User, ct);

    /// <summary>Hồ sơ của tôi; dữ liệu import (họ tên, MSSV, chuyên ngành, lớp, SĐT) chỉ đọc.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.Student)]
    public Task<StudentProfileResponse> Me(CancellationToken ct) => profiles.GetMeAsync(User, ct);

    /// <summary>Sửa 5 kỹ năng, mô tả, công tắc tìm nhóm; phát StudentProfileUpdated.</summary>
    [HttpPut("me")]
    [Authorize(Policy = Policies.Student)]
    public Task<StudentProfileResponse> UpdateMe(StudentProfileInput input, CancellationToken ct) => profiles.UpdateMeAsync(input, User, ct);

    [HttpGet("{id:guid}")]
    public Task<StudentProfileResponse> Get(Guid id, CancellationToken ct) => profiles.GetAsync(id, User, ct);
}
