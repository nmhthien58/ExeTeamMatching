using Academic.Repositories.Catalogs;
using Academic.Services.Catalogs;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Catalogs;

/// <summary>Danh mục chuyên ngành, domain, mã lớp: W12–W14, S14 (BE-21) – academic.yaml tag Catalogs.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class CatalogsController(ICatalogService catalogs) : ControllerBase
{
    [HttpGet("majors")]
    public Task<IReadOnlyList<CatalogItem>> Majors(CancellationToken ct) => catalogs.ListAsync(CatalogKind.Majors, ct);

    [HttpPost("majors")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<CatalogItem>> CreateMajor(CatalogInput input, CancellationToken ct) => Create(CatalogKind.Majors, input, ct);

    [HttpPut("majors/{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<CatalogItem> UpdateMajor(int id, CatalogInput input, CancellationToken ct) => catalogs.UpdateAsync(CatalogKind.Majors, id, input, ct);

    [HttpGet("specializations")]
    public async Task<IReadOnlyList<CatalogItem>> Specializations([FromQuery] int? majorId, CancellationToken ct)
    {
        var list = await catalogs.ListAsync(CatalogKind.Specializations, ct);
        return majorId.HasValue ? list.Where(s => s.MajorId == majorId.Value).ToList() : list;
    }

    [HttpPost("specializations")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<CatalogItem>> CreateSpecialization(CatalogInput input, CancellationToken ct) => Create(CatalogKind.Specializations, input, ct);

    /// <summary>Sửa hoặc vô hiệu (<c>isActive = false</c>).</summary>
    [HttpPut("specializations/{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<CatalogItem> UpdateSpecialization(int id, CatalogInput input, CancellationToken ct) => catalogs.UpdateAsync(CatalogKind.Specializations, id, input, ct);

    [HttpGet("domains")]
    public Task<IReadOnlyList<CatalogItem>> Domains(CancellationToken ct) => catalogs.ListAsync(CatalogKind.Domains, ct);

    [HttpPost("domains")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<CatalogItem>> CreateDomain(CatalogInput input, CancellationToken ct) => Create(CatalogKind.Domains, input, ct);

    [HttpPut("domains/{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<CatalogItem> UpdateDomain(int id, CatalogInput input, CancellationToken ct) => catalogs.UpdateAsync(CatalogKind.Domains, id, input, ct);

    [HttpGet("class-codes")]
    public Task<IReadOnlyList<CatalogItem>> ClassCodes(CancellationToken ct) => catalogs.ListAsync(CatalogKind.ClassCodes, ct);

    /// <summary>Mã lớp chỉ có mã: <c>name</c> và <c>description</c> gửi lên bị bỏ qua, <c>name</c> trả về bằng <c>code</c>.</summary>
    [HttpPost("class-codes")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<CatalogItem>> CreateClassCode(CatalogInput input, CancellationToken ct) => Create(CatalogKind.ClassCodes, input, ct);

    [HttpPut("class-codes/{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<CatalogItem> UpdateClassCode(int id, CatalogInput input, CancellationToken ct) => catalogs.UpdateAsync(CatalogKind.ClassCodes, id, input, ct);

    private async Task<ActionResult<CatalogItem>> Create(CatalogKind kind, CatalogInput input, CancellationToken ct)
    {
        var item = await catalogs.CreateAsync(kind, input, ct);
        return Created($"/api/{CatalogRules.Path(kind)}/{item.Id}", item);
    }
}
