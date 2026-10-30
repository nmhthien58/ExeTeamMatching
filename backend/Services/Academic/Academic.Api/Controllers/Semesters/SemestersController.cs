using Academic.Services.Semesters;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Semesters;

/// <summary>Học kỳ: W03 (BE-16) – academic.yaml tag Semesters.</summary>
[ApiController]
[Authorize]
[Route("api/semesters")]
public sealed class SemestersController(ISemesterService semesters, ICoursePhaseService phases) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SemesterResponse>> List(CancellationToken ct) => semesters.ListAsync(ct);

    /// <summary>Học kỳ hiện hành; 404 khi Bộ môn chưa đặt.</summary>
    [HttpGet("current")]
    public Task<SemesterResponse> Current(CancellationToken ct) => semesters.GetCurrentAsync(ct);

    /// <summary>Giai đoạn của EXE101 và EXE201 (rule 18): client đổi tab Khám phá ↔ Dự án (BE-67).</summary>
    [HttpGet("current/phase")]
    public Task<IReadOnlyList<CoursePhaseResponse>> CurrentPhase(CancellationToken ct) => phases.GetCurrentAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<SemesterResponse>> Create(SemesterInput input, CancellationToken ct)
    {
        var semester = await semesters.CreateAsync(input, ct);
        return Created($"/api/semesters/{semester.Id}", semester);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<SemesterResponse> Update(Guid id, SemesterInput input, CancellationToken ct) => semesters.UpdateAsync(id, input, ct);
}
