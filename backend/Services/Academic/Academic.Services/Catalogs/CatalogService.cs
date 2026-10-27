using System.Text.RegularExpressions;
using Academic.Repositories.Catalogs;
using Academic.Services.Caching;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academic.Services.Catalogs;

public interface ICatalogService
{
    /// <summary>Cả dòng đang dùng và đã vô hiệu (client lọc theo <c>isActive</c>), sắp theo mã. Đọc qua cache Redis.</summary>
    Task<IReadOnlyList<CatalogItem>> ListAsync(CatalogKind kind, CancellationToken ct);

    Task<CatalogItem> CreateAsync(CatalogKind kind, CatalogInput input, CancellationToken ct);

    Task<CatalogItem> UpdateAsync(CatalogKind kind, int id, CatalogInput input, CancellationToken ct);
}

/// <summary>CRUD danh mục + cache Redis (BE-21, W12–W14, rule 12). Không có xóa: vô hiệu bằng <c>isActive = false</c>.</summary>
public sealed class CatalogService(ICatalogRepository catalogs, AcademicCache cache) : ICatalogService
{
    public Task<IReadOnlyList<CatalogItem>> ListAsync(CatalogKind kind, CancellationToken ct) =>
        cache.GetOrLoadAsync(CacheKey(kind), async token =>
            (IReadOnlyList<CatalogItem>)(await catalogs.ListAsync(kind, token)).Select(ToItem).ToList(), ct);

    public async Task<CatalogItem> CreateAsync(CatalogKind kind, CatalogInput input, CancellationToken ct)
    {
        var row = CatalogRules.Validate(kind, input, id: 0, isActive: input.IsActive ?? true);
        await EnsureCodeFreeAsync(kind, row, ct);

        var saved = await SaveAsync(() => catalogs.AddAsync(kind, row, ct));
        await cache.InvalidateAsync(CacheKey(kind), ct);
        return ToItem(saved);
    }

    public async Task<CatalogItem> UpdateAsync(CatalogKind kind, int id, CatalogInput input, CancellationToken ct)
    {
        var current = await catalogs.GetAsync(kind, id, ct) ?? throw CatalogErrors.NotFound(kind, id);
        var row = CatalogRules.Validate(kind, input, id, input.IsActive ?? current.IsActive);
        await EnsureCodeFreeAsync(kind, row, ct);

        var saved = await SaveAsync(() => catalogs.UpdateAsync(kind, row, ct)) ?? throw CatalogErrors.NotFound(kind, id);
        await cache.InvalidateAsync(CacheKey(kind), ct);
        return ToItem(saved);
    }

    public static string CacheKey(CatalogKind kind) => AcademicCacheKeys.Catalog(CatalogRules.Path(kind));

    private async Task EnsureCodeFreeAsync(CatalogKind kind, CatalogRow row, CancellationToken ct)
    {
        if (await catalogs.CodeExistsAsync(kind, row.Code, row.Id == 0 ? null : row.Id, ct))
        {
            throw CatalogErrors.CodeTaken(row.Code);
        }
    }

    /// <summary>Hai Admin thêm cùng mã một lúc: bước kiểm tra ở trên cùng qua, DB chặn bằng <c>uq_*_code</c>.</summary>
    private static async Task<T> SaveAsync<T>(Func<Task<T>> save)
    {
        try
        {
            return await save();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw CatalogErrors.CodeTaken(null);
        }
    }

    private static CatalogItem ToItem(CatalogRow row) => new(row.Id, row.Code, row.Name, row.Description, row.IsActive);
}

/// <summary>Kiểm tra dữ liệu theo ràng buộc CHECK của deploy/db/02_academic_db.sql, trước khi ghi DB.</summary>
public static partial class CatalogRules
{
    public static string Path(CatalogKind kind) => kind switch
    {
        CatalogKind.Specializations => "specializations",
        CatalogKind.Domains => "domains",
        _ => "class-codes",
    };

    /// <summary>Chuẩn hóa (cắt khoảng trắng, mã viết hoa) và kiểm tra; lỗi ném 400 theo field.</summary>
    public static CatalogRow Validate(CatalogKind kind, CatalogInput input, int id, bool isActive)
    {
        var errors = new Dictionary<string, string[]>();
        var code = input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var name = input.Name?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();

        var (codePattern, codeHint) = kind switch
        {
            CatalogKind.Specializations => (SpecializationCode(), "2–20 ký tự: chữ in hoa, số hoặc _"),
            CatalogKind.Domains => (DomainCode(), "D01 đến D18"),
            _ => (ClassCode(), "3–20 ký tự: chữ in hoa hoặc số"),
        };
        if (code.Length == 0)
        {
            errors["code"] = ["Nhập mã."];
        }
        else if (!codePattern.IsMatch(code))
        {
            errors["code"] = [$"Mã không hợp lệ ({codeHint})."];
        }

        if (kind == CatalogKind.ClassCodes)
        {
            // Bảng class_codes chỉ có mã: tên hiển thị chính là mã.
            name = code;
            description = null;
        }
        else
        {
            var maxName = kind == CatalogKind.Domains ? 150 : 100;
            if (name.Length == 0)
            {
                errors["name"] = ["Nhập tên."];
            }
            else if (name.Length > maxName)
            {
                errors["name"] = [$"Tên tối đa {maxName} ký tự."];
            }
        }

        if (kind == CatalogKind.Specializations)
        {
            description = null;
        }
        else if (description is { Length: > 500 })
        {
            errors["description"] = ["Mô tả tối đa 500 ký tự."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
        return new CatalogRow(id, code, name, description, isActive);
    }

    [GeneratedRegex("^[A-Z0-9_]{2,20}$")]
    private static partial Regex SpecializationCode();

    [GeneratedRegex("^D(0[1-9]|1[0-8])$")]
    private static partial Regex DomainCode();

    [GeneratedRegex("^[A-Z0-9]{3,20}$")]
    private static partial Regex ClassCode();
}

/// <summary>Lỗi của module Catalogs.</summary>
public static class CatalogErrors
{
    public static NotFoundException NotFound(CatalogKind kind, int id) => new(kind switch
    {
        CatalogKind.Specializations => "chuyên ngành",
        CatalogKind.Domains => "domain",
        _ => "mã lớp",
    }, id);

    /// <summary>Trùng mã trả 400 theo field <c>code</c> (lỗi validate), không thêm mã lỗi mới vào docs/contracts/README.md.</summary>
    public static RequestValidationException CodeTaken(string? code) =>
        new(new Dictionary<string, string[]> { ["code"] = [code is null ? "Mã đã tồn tại." : $"Mã {code} đã tồn tại."] });
}
