using Academic.Services;
using Academic.Services.Mentors;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Mentors;

/// <summary>Mentor: W27, W28 (Admin), M01 (mentor) – BE-66, academic.yaml tag Mentors.</summary>
[ApiController]
[Authorize]
[Route("api/mentors")]
public sealed class MentorsController(IMentorService mentors) : ControllerBase
{
    /// <summary>Admin xem W27; Team đọc bằng token dịch vụ khi ghép mentor, giống <c>GET /api/lecturers</c>.</summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Service}")]
    public Task<PagedResponse<MentorResponse>> List([FromQuery] MentorQuery query, CancellationToken ct) => mentors.ListAsync(query, ct);

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<MentorResponse>> Create(MentorInput input, CancellationToken ct)
    {
        var mentor = await mentors.CreateAsync(input, User, ct);
        return Created($"/api/mentors/{mentor.Id}", mentor);
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.Mentor)]
    public Task<MentorResponse> Me(CancellationToken ct) => mentors.GetMeAsync(User, ct);

    /// <summary>Mentor sửa số nhóm mong muốn, giới thiệu, domain; phát MentorRegistered.</summary>
    [HttpPut("me")]
    [Authorize(Policy = Policies.Mentor)]
    public Task<MentorResponse> UpdateMe(MentorProfileInput input, CancellationToken ct) => mentors.UpdateMeAsync(input, User, ct);

    [HttpGet("{id:guid}")]
    public Task<MentorResponse> Get(Guid id, CancellationToken ct) => mentors.GetAsync(id, User, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<MentorResponse> Update(Guid id, MentorInput input, CancellationToken ct) => mentors.UpdateAsync(id, input, ct);
}
