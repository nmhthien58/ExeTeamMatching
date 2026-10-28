using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Export;

namespace Team.Api.Controllers.Export;

/// <summary>Xuất Excel danh sách nhóm: W15, W22 (BE-50) – team.yaml tag Admin.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/teams")]
public sealed class ExportController(IExportService export) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TeamExportQuery query, CancellationToken ct)
    {
        var file = await export.ExportTeamsAsync(query, ct);
        return File(file.Content, ExportFile.ContentType, file.FileName);
    }
}
