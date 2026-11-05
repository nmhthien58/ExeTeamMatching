using Identity.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Identity.Repositories.ClientErrors;

/// <summary>Điều kiện lọc của W30. Trường null là không lọc; <see cref="To"/> không tính mốc đó.</summary>
public sealed record ClientErrorFilter(string? Kind, DateTimeOffset? From, DateTimeOffset? To, string? AppVersion);

/// <summary>Một lần lỗi trong khoảng thống kê, chỉ đủ để đếm theo ngày.</summary>
public sealed record ClientErrorOccurrence(DateTimeOffset OccurredAt, string Kind);

/// <summary>Nhóm lỗi lặp lại: cùng <c>exception_type</c> và dòng đầu của stack.</summary>
public sealed record ClientErrorGroup(string ExceptionType, string FirstStackLine, int Count, int FatalCount, DateTimeOffset LastOccurredAt);

public interface IClientErrorReportRepository
{
    Task AddAsync(ClientErrorReport report, CancellationToken ct);

    /// <summary>Mới nhất trước.</summary>
    Task<(List<ClientErrorReport> Items, int TotalCount)> SearchAsync(ClientErrorFilter filter, int page, int pageSize, CancellationToken ct);

    Task<List<ClientErrorOccurrence>> ListOccurrencesAsync(DateTimeOffset from, CancellationToken ct);

    /// <summary>Số <c>user_id</c> khác nhau; báo cáo trước đăng nhập (null) không tính.</summary>
    Task<int> CountAffectedUsersAsync(DateTimeOffset from, CancellationToken ct);

    Task<List<ClientErrorGroup>> ListTopGroupsAsync(DateTimeOffset from, int take, CancellationToken ct);
}

public sealed class ClientErrorReportRepository(IdentityDbContext db) : IClientErrorReportRepository
{
    public async Task AddAsync(ClientErrorReport report, CancellationToken ct)
    {
        db.ClientErrorReports.Add(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(List<ClientErrorReport> Items, int TotalCount)> SearchAsync(ClientErrorFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ClientErrorReports.AsNoTracking();
        if (filter.Kind is not null)
        {
            query = query.Where(x => x.Kind == filter.Kind);
        }
        if (filter.From is { } from)
        {
            query = query.Where(x => x.OccurredAt >= from);
        }
        if (filter.To is { } to)
        {
            query = query.Where(x => x.OccurredAt < to);
        }
        if (filter.AppVersion is not null)
        {
            query = query.Where(x => x.AppVersion == filter.AppVersion);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<ClientErrorOccurrence>> ListOccurrencesAsync(DateTimeOffset from, CancellationToken ct) =>
        db.ClientErrorReports.AsNoTracking()
            .Where(x => x.OccurredAt >= from)
            .Select(x => new ClientErrorOccurrence(x.OccurredAt, x.Kind))
            .ToListAsync(ct);

    public Task<int> CountAffectedUsersAsync(DateTimeOffset from, CancellationToken ct) =>
        db.ClientErrorReports.AsNoTracking()
            .Where(x => x.OccurredAt >= from && x.UserId != null)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(ct);

    public async Task<List<ClientErrorGroup>> ListTopGroupsAsync(DateTimeOffset from, int take, CancellationToken ct)
    {
        // Sắp xếp trên kiểu ẩn danh: EF không dịch được OrderBy theo thuộc tính của record dựng bằng constructor.
        var groups = await db.ClientErrorReports.AsNoTracking()
            .Where(x => x.OccurredAt >= from)
            .GroupBy(x => new { x.ExceptionType, x.FirstStackLine })
            .Select(g => new
            {
                g.Key.ExceptionType,
                g.Key.FirstStackLine,
                Count = g.Count(),
                FatalCount = g.Count(x => x.Kind == ClientErrorKinds.Fatal),
                LastOccurredAt = g.Max(x => x.OccurredAt),
            })
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.LastOccurredAt)
            .Take(take)
            .ToListAsync(ct);
        return groups.Select(g => new ClientErrorGroup(g.ExceptionType, g.FirstStackLine, g.Count, g.FatalCount, g.LastOccurredAt)).ToList();
    }
}
