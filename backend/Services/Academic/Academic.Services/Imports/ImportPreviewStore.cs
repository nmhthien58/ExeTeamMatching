using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Academic.Services.Imports;

/// <summary>Bản xem trước giữ giữa hai bước preview và confirm: các dòng đọc từ file (chưa chuẩn hóa) và lựa chọn của Admin.</summary>
public sealed record StoredPreview(Guid SemesterId, string CourseCode, string FileName, Guid UploadedBy, IReadOnlyList<ImportRow> Rows);

public sealed record StoredLecturerPreview(string FileName, Guid UploadedBy, IReadOnlyList<LecturerImportRow> Rows);

/// <summary>
/// Giữ bản xem trước trong Redis 30 phút (schema <c>ImportPreview.previewId</c>), key <c>academic:imports:preview:{id}</c>.
/// Khác <see cref="Caching.AcademicCache"/>: đây là dữ liệu chính của bước confirm chứ không phải cache, nên Redis lỗi thì request lỗi.
/// </summary>
public sealed class ImportPreviewStore(IDistributedCache cache)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task SaveAsync(Guid previewId, StoredPreview preview, CancellationToken ct) =>
        cache.SetAsync(Key(previewId), JsonSerializer.SerializeToUtf8Bytes(preview, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);

    public async Task<StoredPreview?> GetAsync(Guid previewId, CancellationToken ct) =>
        await cache.GetAsync(Key(previewId), ct) is { } bytes ? JsonSerializer.Deserialize<StoredPreview>(bytes, Json) : null;

    public Task RemoveAsync(Guid previewId, CancellationToken ct) => cache.RemoveAsync(Key(previewId), ct);

    public Task SaveLecturerAsync(Guid previewId, StoredLecturerPreview preview, CancellationToken ct) =>
        cache.SetAsync(LecturerKey(previewId), JsonSerializer.SerializeToUtf8Bytes(preview, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);

    public async Task<StoredLecturerPreview?> GetLecturerAsync(Guid previewId, CancellationToken ct) =>
        await cache.GetAsync(LecturerKey(previewId), ct) is { } bytes ? JsonSerializer.Deserialize<StoredLecturerPreview>(bytes, Json) : null;

    public Task RemoveLecturerAsync(Guid previewId, CancellationToken ct) => cache.RemoveAsync(LecturerKey(previewId), ct);

    private static string Key(Guid previewId) => $"imports:preview:{previewId}";

    private static string LecturerKey(Guid previewId) => $"imports:lecturers:preview:{previewId}";
}
