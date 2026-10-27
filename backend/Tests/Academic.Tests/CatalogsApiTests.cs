using System.Net;
using System.Net.Http.Json;
using Academic.Repositories.Catalogs;
using Academic.Services.Catalogs;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Tests;

/// <summary>
/// API danh mục (BE-21) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c> sau
/// <c>docker compose -f deploy/docker-compose.yml up -d postgres redis</c>. Dòng thêm mới có mã ngẫu nhiên và được xóa khi xong;
/// domain sửa thử được trả lại như cũ.
/// </summary>
public sealed class CatalogsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly string _suffix = Random.Shared.Next(0, 1_000_000).ToString("D6");
    private readonly List<int> _specializations = [];
    private readonly List<int> _classCodes = [];

    [Fact]
    public async Task Any_signed_in_user_reads_catalogs_but_anonymous_cannot()
    {
        if (!Enabled)
        {
            return;
        }

        var domains = await _api.ClientAs(Roles.Student).GetFromJsonAsync<List<CatalogItem>>("/api/domains");
        Assert.Equal(18, domains!.Count);
        Assert.Equal("D01", domains[0].Code);
        Assert.NotEmpty((await _api.ClientAs(Roles.Lecturer).GetFromJsonAsync<List<CatalogItem>>("/api/specializations"))!);
        Assert.All((await _api.ClientAs(Roles.Service).GetFromJsonAsync<List<CatalogItem>>("/api/class-codes"))!, c => Assert.Equal(c.Code, c.Name));

        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/domains")).StatusCode);
    }

    [Fact]
    public async Task Admin_creates_specialization_and_cached_list_is_refreshed()
    {
        if (!Enabled)
        {
            return;
        }

        var admin = _api.ClientAs(Roles.Admin);
        await admin.GetFromJsonAsync<List<CatalogItem>>("/api/specializations"); // nạp cache
        Assert.NotNull(await CachedAsync(CatalogKind.Specializations));

        var response = await admin.PostAsJsonAsync("/api/specializations", new { code = $"t_{_suffix}", name = "Chuyên ngành thử" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CatalogItem>())!;
        _specializations.Add(created.Id);
        Assert.Equal(($"T_{_suffix}", "Chuyên ngành thử", true), (created.Code, created.Name, created.IsActive));
        Assert.Equal($"/api/specializations/{created.Id}", response.Headers.Location!.ToString());
        Assert.Null(await CachedAsync(CatalogKind.Specializations)); // POST đã xóa key

        var list = await admin.GetFromJsonAsync<List<CatalogItem>>("/api/specializations");
        Assert.Contains(list!, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Admin_deactivates_class_code_and_duplicate_code_is_rejected()
    {
        if (!Enabled)
        {
            return;
        }

        var admin = _api.ClientAs(Roles.Admin);
        var created = (await (await admin.PostAsJsonAsync("/api/class-codes", new { code = $"ZT{_suffix}", name = "bỏ qua" }))
            .Content.ReadFromJsonAsync<CatalogItem>())!;
        _classCodes.Add(created.Id);

        var updated = await admin.PutAsJsonAsync($"/api/class-codes/{created.Id}", new { code = $"ZT{_suffix}", name = "x", isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var list = await admin.GetFromJsonAsync<List<CatalogItem>>("/api/class-codes");
        Assert.False(list!.Single(c => c.Id == created.Id).IsActive);

        var duplicate = await admin.PostAsJsonAsync("/api/class-codes", new { code = $"zt{_suffix}", name = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("\"code\"", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_renames_domain_keeping_status_when_isActive_is_omitted()
    {
        if (!Enabled)
        {
            return;
        }

        var admin = _api.ClientAs(Roles.Admin);
        var original = (await admin.GetFromJsonAsync<List<CatalogItem>>("/api/domains"))!.Single(d => d.Code == "D18");
        try
        {
            var response = await admin.PutAsJsonAsync($"/api/domains/{original.Id}", new { code = "D18", name = "Tên thử", description = "Mô tả thử" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var renamed = (await admin.GetFromJsonAsync<List<CatalogItem>>("/api/domains"))!.Single(d => d.Id == original.Id);
            Assert.Equal(("Tên thử", "Mô tả thử", original.IsActive), (renamed.Name, renamed.Description, renamed.IsActive));
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/domains/{original.Id}",
                new { code = original.Code, name = original.Name, description = original.Description, isActive = original.IsActive });
        }
    }

    [Fact]
    public async Task Non_admin_cannot_change_catalogs_and_unknown_id_is_404()
    {
        if (!Enabled)
        {
            return;
        }

        var student = await _api.ClientAs(Roles.Student).PostAsJsonAsync("/api/class-codes", new { code = $"ZT{_suffix}", name = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);

        var missing = await _api.ClientAs(Roles.Admin).PutAsJsonAsync("/api/specializations/999999", new { code = "XX", name = "x" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<byte[]?> CachedAsync(CatalogKind kind)
    {
        using var scope = _api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDistributedCache>().GetAsync(CatalogService.CacheKey(kind));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_specializations.Count + _classCodes.Count > 0)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.Specializations.Where(s => _specializations.Contains(s.Id)).ExecuteDeleteAsync();
                return await db.ClassCodes.Where(c => _classCodes.Contains(c.Id)).ExecuteDeleteAsync();
            });
            // Danh sách trong cache còn dòng vừa xóa: bỏ key để lần đọc sau lấy lại từ DB.
            using var scope = _api.Services.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            await cache.RemoveAsync(CatalogService.CacheKey(CatalogKind.Specializations));
            await cache.RemoveAsync(CatalogService.CacheKey(CatalogKind.ClassCodes));
        }
        await _api.DisposeAsync();
    }
}
