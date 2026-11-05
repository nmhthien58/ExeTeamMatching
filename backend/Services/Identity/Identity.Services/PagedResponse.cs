namespace Identity.Services;

/// <summary>Kết quả phân trang (docs/contracts/README.md): <c>{ items, page, pageSize, totalCount, totalPages }</c>.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount, (totalCount + pageSize - 1) / pageSize);
}
