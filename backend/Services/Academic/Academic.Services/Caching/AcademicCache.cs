using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Academic.Services.Caching;

/// <summary>Key cache của Academic trong Redis (tiền tố <c>academic:</c> đặt ở <see cref="ServiceCollectionExtensions"/>).</summary>
public static class AcademicCacheKeys
{
    public static string Catalog(string path) => $"catalog:{path}";

    /// <summary>Học kỳ hiện hành (Team gọi trước mỗi thao tác lập nhóm).</summary>
    public const string CurrentSemester = "semesters:current";

    /// <summary>Mọi đợt đang mở (trạng thái OPEN) của mọi môn.</summary>
    public const string OpenRounds = "rounds:open";
}

/// <summary>
/// Cache-aside trên Redis (PROJECT_CONTEXT §5.3: cache danh mục và trạng thái đợt đang mở, xóa khi thay đổi).
/// Redis lỗi thì đọc thẳng DB và ghi log cảnh báo – cache không bao giờ làm request thất bại.
/// </summary>
public sealed class AcademicCache(IDistributedCache cache, ILogger<AcademicCache> logger)
{
    /// <summary>Hạn tối đa của một key: phòng khi xóa cache lỗi thì dữ liệu cũ cũng không sống quá lâu.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrLoadAsync<T>(string key, Func<CancellationToken, Task<T>> load, CancellationToken ct)
    {
        try
        {
            if (await cache.GetAsync(key, ct) is { } cached)
            {
                return JsonSerializer.Deserialize<T>(cached, Json)!;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đọc được cache {Key}, đọc từ DB", key);
            return await load(ct);
        }

        var value = await load(ct);
        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không ghi được cache {Key}", key);
        }
        return value;
    }

    /// <summary>Gọi sau khi dữ liệu đã lưu xuống DB.</summary>
    public async Task InvalidateAsync(string key, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Không xóa được cache {Key}; dữ liệu cũ còn tối đa {Ttl}", key, Ttl);
        }
    }
}
