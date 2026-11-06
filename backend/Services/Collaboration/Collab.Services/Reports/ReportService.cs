using System.Text.RegularExpressions;
using BuildingBlocks.Errors;
using Collab.Services.Files;
using Collab.Services.Storage;
using Microsoft.Extensions.Options;

namespace Collab.Services.Reports;

/// <summary>Một báo cáo PDF trong <c>reports/{ownerId}/</c>. <see cref="FileName"/> do server đặt.</summary>
public sealed record ReportResponse(string FileName, long SizeBytes, DateTimeOffset CreatedAt);

/// <summary>Kết quả tải lên: thông tin báo cáo kèm link ký sẵn.</summary>
public sealed record UploadedReportResponse(string FileName, long SizeBytes, DateTimeOffset CreatedAt, string Url, DateTimeOffset ExpiresAt);

public sealed record UploadReportCommand(string FileName, long SizeBytes, Stream Content);

public interface IReportService
{
    Task<UploadedReportResponse> UploadAsync(Guid ownerId, Guid callerId, UploadReportCommand command, CancellationToken ct);

    /// <summary>Mới nhất trước.</summary>
    Task<IReadOnlyList<ReportResponse>> ListAsync(Guid ownerId, Guid callerId, CancellationToken ct);

    Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid ownerId, string fileName, Guid callerId, CancellationToken ct);
}

/// <summary>
/// Báo cáo dự án PDF của từng người (MB-53). Không có bảng riêng: đường dẫn S3 chứa chủ báo cáo, và mọi thao tác đều yêu cầu
/// <c>ownerId</c> trùng id trong JWT – tương đương <c>match /reports/{uid}/{file}</c> của Firebase Storage (docs/file-storage.md).
/// </summary>
public sealed partial class ReportService(IObjectStorage storage, IOptions<ObjectStorageOptions> options, TimeProvider clock) : IReportService
{
    private const string ContentType = "application/pdf";
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task<UploadedReportResponse> UploadAsync(Guid ownerId, Guid callerId, UploadReportCommand command, CancellationToken ct)
    {
        RequireOwner(ownerId, callerId);
        var (name, contentType) = FileRules.Validate(command.FileName, command.SizeBytes);
        if (contentType != ContentType || !await StartsWithPdfSignatureAsync(command.Content, ct))
        {
            throw new HttpStatusException(415, "file-type-not-allowed", "Định dạng file không được hỗ trợ", "Báo cáo phải là file PDF.");
        }

        var now = clock.GetUtcNow();
        var fileName = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.pdf";
        var key = Key(ownerId, fileName);
        await storage.PutAsync(key, command.Content, ContentType, name, ct);
        return new UploadedReportResponse(fileName, command.SizeBytes, now, storage.GetDownloadUrl(key), ExpiresAt());
    }

    public async Task<IReadOnlyList<ReportResponse>> ListAsync(Guid ownerId, Guid callerId, CancellationToken ct)
    {
        RequireOwner(ownerId, callerId);
        var objects = await storage.ListAsync(Prefix(ownerId), ct);
        return objects
            .OrderByDescending(o => o.LastModified)
            .Select(o => new ReportResponse(o.Key[Prefix(ownerId).Length..], o.SizeBytes, o.LastModified))
            .ToList();
    }

    public async Task<DownloadUrlResponse> GetDownloadUrlAsync(Guid ownerId, string fileName, Guid callerId, CancellationToken ct)
    {
        RequireOwner(ownerId, callerId);
        var key = Key(ownerId, fileName);
        if (!ReportFileName().IsMatch(fileName) || !(await storage.ListAsync(key, ct)).Any(o => o.Key == key))
        {
            throw new NotFoundException("báo cáo", fileName);
        }
        return new DownloadUrlResponse(storage.GetDownloadUrl(key), ExpiresAt());
    }

    private DateTimeOffset ExpiresAt() => clock.GetUtcNow().AddMinutes(options.Value.PresignMinutes);

    private static string Prefix(Guid ownerId) => $"reports/{ownerId}/";

    private static string Key(Guid ownerId, string fileName) => Prefix(ownerId) + fileName;

    private static void RequireOwner(Guid ownerId, Guid callerId)
    {
        if (ownerId != callerId)
        {
            throw new ForbiddenException("not-report-owner", "Không có quyền với báo cáo này", "Chỉ chủ báo cáo mới đọc hoặc ghi được.");
        }
    }

    private static async Task<bool> StartsWithPdfSignatureAsync(Stream content, CancellationToken ct)
    {
        var head = new byte[PdfSignature.Length];
        var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        content.Position = 0;
        return read == head.Length && head.AsSpan().SequenceEqual(PdfSignature);
    }

    [GeneratedRegex(@"^\d{8}-\d{6}-[0-9a-f]{8}\.pdf$")]
    private static partial Regex ReportFileName();
}
