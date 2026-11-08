using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Catalogs;

/// <summary>Bốn danh mục của rule 12: ngành, chuyên ngành, domain, mã lớp (W12–W14, BE-91).</summary>
public enum CatalogKind
{
    Majors,
    Specializations,
    Domains,
    ClassCodes,
}

/// <summary>Một dòng danh mục ở dạng chung. Mã lớp không có cột tên nên <see cref="Name"/> = <see cref="Code"/>; chỉ domain có mô tả.</summary>
public sealed record CatalogRow(int Id, string Code, string Name, string? Description, bool IsActive, int? MajorId = null);

public interface ICatalogRepository
{
    Task<IReadOnlyList<CatalogRow>> ListAsync(CatalogKind kind, CancellationToken ct);

    Task<CatalogRow?> GetAsync(CatalogKind kind, int id, CancellationToken ct);

    Task<bool> CodeExistsAsync(CatalogKind kind, string code, int? exceptId, CancellationToken ct);

    Task<CatalogRow> AddAsync(CatalogKind kind, CatalogRow row, CancellationToken ct);

    /// <summary>Ghi đè mã, tên, mô tả, trạng thái. Trả null khi không có dòng <paramref name="row"/>.Id.</summary>
    Task<CatalogRow?> UpdateAsync(CatalogKind kind, CatalogRow row, CancellationToken ct);
}

public sealed class CatalogRepository(AcademicDbContext db) : ICatalogRepository
{
    public async Task<IReadOnlyList<CatalogRow>> ListAsync(CatalogKind kind, CancellationToken ct) =>
        (await Rows(kind).OrderBy(x => x.Code).ToListAsync(ct)).Select(x => x.ToRow()).ToList();

    public async Task<CatalogRow?> GetAsync(CatalogKind kind, int id, CancellationToken ct) =>
        (await Rows(kind).SingleOrDefaultAsync(x => x.Id == id, ct))?.ToRow();

    public Task<bool> CodeExistsAsync(CatalogKind kind, string code, int? exceptId, CancellationToken ct) =>
        Rows(kind).AnyAsync(x => x.Code == code && x.Id != exceptId, ct);

    public async Task<CatalogRow> AddAsync(CatalogKind kind, CatalogRow row, CancellationToken ct)
    {
        switch (kind)
        {
            case CatalogKind.Majors:
                var major = new Major { Code = row.Code, Name = row.Name, IsActive = row.IsActive };
                db.Majors.Add(major);
                await db.SaveChangesAsync(ct);
                return ToRow(major);
            case CatalogKind.Specializations:
                var specialization = new Specialization { Code = row.Code, Name = row.Name, IsActive = row.IsActive, MajorId = row.MajorId };
                db.Specializations.Add(specialization);
                await db.SaveChangesAsync(ct);
                return ToRow(specialization);
            case CatalogKind.Domains:
                var domain = new Domain { Code = row.Code, Name = row.Name, Description = row.Description, IsActive = row.IsActive };
                db.Domains.Add(domain);
                await db.SaveChangesAsync(ct);
                return ToRow(domain);
            default:
                var classCode = new ClassCode { Code = row.Code, IsActive = row.IsActive };
                db.ClassCodes.Add(classCode);
                await db.SaveChangesAsync(ct);
                return ToRow(classCode);
        }
    }

    public async Task<CatalogRow?> UpdateAsync(CatalogKind kind, CatalogRow row, CancellationToken ct)
    {
        switch (kind)
        {
            case CatalogKind.Majors:
                if (await db.Majors.FindAsync([row.Id], ct) is not { } major)
                {
                    return null;
                }
                (major.Code, major.Name, major.IsActive) = (row.Code, row.Name, row.IsActive);
                await db.SaveChangesAsync(ct);
                return ToRow(major);
            case CatalogKind.Specializations:
                if (await db.Specializations.FindAsync([row.Id], ct) is not { } specialization)
                {
                    return null;
                }
                (specialization.Code, specialization.Name, specialization.IsActive) = (row.Code, row.Name, row.IsActive);
                if (row.MajorId.HasValue)
                {
                    specialization.MajorId = row.MajorId.Value;
                }
                await db.SaveChangesAsync(ct);
                return ToRow(specialization);
            case CatalogKind.Domains:
                if (await db.Domains.FindAsync([row.Id], ct) is not { } domain)
                {
                    return null;
                }
                (domain.Code, domain.Name, domain.Description, domain.IsActive) = (row.Code, row.Name, row.Description, row.IsActive);
                await db.SaveChangesAsync(ct);
                return ToRow(domain);
            default:
                if (await db.ClassCodes.FindAsync([row.Id], ct) is not { } classCode)
                {
                    return null;
                }
                (classCode.Code, classCode.IsActive) = (row.Code, row.IsActive);
                await db.SaveChangesAsync(ct);
                return ToRow(classCode);
        }
    }

    /// <summary>Chiếu sang <see cref="RowView"/> (property init) để EF còn dịch được Where / OrderBy phía sau sang SQL.</summary>
    private IQueryable<RowView> Rows(CatalogKind kind) => kind switch
    {
        CatalogKind.Majors => db.Majors.AsNoTracking()
            .Select(x => new RowView { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive }),
        CatalogKind.Specializations => db.Specializations.AsNoTracking()
            .Select(x => new RowView { Id = x.Id, Code = x.Code, Name = x.Name, IsActive = x.IsActive, MajorId = x.MajorId }),
        CatalogKind.Domains => db.Domains.AsNoTracking()
            .Select(x => new RowView { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description, IsActive = x.IsActive }),
        _ => db.ClassCodes.AsNoTracking()
            .Select(x => new RowView { Id = x.Id, Code = x.Code, Name = x.Code, IsActive = x.IsActive }),
    };

    private sealed class RowView
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsActive { get; init; }
        public int? MajorId { get; init; }

        public CatalogRow ToRow() => new(Id, Code, Name, Description, IsActive, MajorId);
    }

    private static CatalogRow ToRow(Major x) => new(x.Id, x.Code, x.Name, null, x.IsActive);

    private static CatalogRow ToRow(Specialization x) => new(x.Id, x.Code, x.Name, null, x.IsActive, x.MajorId);

    private static CatalogRow ToRow(Domain x) => new(x.Id, x.Code, x.Name, x.Description, x.IsActive);

    private static CatalogRow ToRow(ClassCode x) => new(x.Id, x.Code, x.Code, null, x.IsActive);
}
