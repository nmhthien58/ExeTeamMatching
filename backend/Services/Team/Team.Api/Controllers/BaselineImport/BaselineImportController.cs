using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Team.Services.BaselineImport;

namespace Team.Api.Controllers.BaselineImport;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/teams")]
public sealed class BaselineImportController(IBaselineImportService baselineImport) : ControllerBase
{
    /// <summary>
    /// Admin import file nhóm EXE101 làm mốc, tạo nhóm EXE201 kế thừa (W06).
    /// </summary>
    [HttpPost("baseline-import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BaselineImportResponse>> Import(
        IFormFile file,
        [FromForm] Guid semesterId,
        [FromForm] bool dryRun = true,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return Ok(new BaselineImportResponse(0, 0, 0, 0, [new BaselineImportRowError(1, "File tải lên không được rỗng.")]));
        }

        using var stream = file.OpenReadStream();
        var result = await baselineImport.ImportAsync(semesterId, stream, file.FileName, dryRun, ct);
        return Ok(result);
    }

    /// <summary>
    /// Tự động đối chiếu và kế thừa nhóm EXE201 từ EXE101 kỳ trước (BE-92).
    /// </summary>
    [HttpPost("auto-inherit")]
    public async Task<ActionResult<AutoInheritResponse>> AutoInherit(
        [FromBody] AutoInheritRequest request,
        CancellationToken ct = default)
    {
        var result = await baselineImport.AutoInheritAsync(request, ct);
        return Ok(result);
    }
}
