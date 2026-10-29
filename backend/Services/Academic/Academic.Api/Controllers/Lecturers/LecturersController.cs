using Academic.Services;
using Academic.Services.Lecturers;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Lecturers;

/// <summary>Giảng viên: W11 (Admin), S23, S24 (sinh viên), L06 (giảng viên) – BE-20, academic.yaml tag Lecturers.</summary>
[ApiController]
[Authorize]
[Route("api/lecturers")]
public sealed class LecturersController(ILecturerService lecturers) : ControllerBase
{
    /// <summary>Admin thấy mọi giảng viên kèm SĐT; người khác chỉ thấy giảng viên đang hoạt động, không có SĐT.</summary>
    [HttpGet]
    public Task<PagedResponse<LecturerResponse>> List([FromQuery] LecturerQuery query, CancellationToken ct) => lecturers.ListAsync(query, User, ct);

    /// <summary>Thêm giảng viên; phát LecturerRegistered để Identity cấp quyền đăng nhập.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<LecturerResponse>> Create(LecturerInput input, CancellationToken ct)
    {
        var lecturer = await lecturers.CreateAsync(input, ct);
        return Created($"/api/lecturers/{lecturer.Id}", lecturer);
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.Lecturer)]
    public Task<LecturerResponse> Me(CancellationToken ct) => lecturers.GetMeAsync(User, ct);

    /// <summary>Giảng viên sửa domain chuyên môn; phát LecturerProfileUpdated.</summary>
    [HttpPut("me")]
    [Authorize(Policy = Policies.Lecturer)]
    public Task<LecturerResponse> UpdateMe(LecturerProfileInput input, CancellationToken ct) => lecturers.UpdateMeAsync(input, User, ct);

    [HttpGet("{id:guid}")]
    public Task<LecturerResponse> Get(Guid id, CancellationToken ct) => lecturers.GetAsync(id, User, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<LecturerResponse> Update(Guid id, LecturerInput input, CancellationToken ct) => lecturers.UpdateAsync(id, input, ct);
}
