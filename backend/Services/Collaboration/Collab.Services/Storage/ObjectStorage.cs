using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collab.Services.Storage;

/// <summary>
/// Cấu hình kho file S3 (section <c>ObjectStorage</c>). Dev chạy SeaweedFS trong Docker (deploy/docker-compose.yml);
/// máy chủ nào tương thích S3 cũng dùng được.
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Địa chỉ service gọi tới (trong Docker: <c>http://object-storage:8333</c>).</summary>
    public string ServiceUrl { get; set; } = "http://localhost:9000";

    /// <summary>Địa chỉ trình duyệt / app mở được – dùng để ký URL tải xuống. Để trống = <see cref="ServiceUrl"/>.</summary>
    public string? PublicUrl { get; set; }

    public string Bucket { get; set; } = "exe-files";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Thời hạn URL ký sẵn (phút).</summary>
    public int PresignMinutes { get; set; } = 10;
}

/// <summary>
/// Lưu và lấy file của workspace (rule 24). Key theo mẫu <see cref="FileKey"/>; file chỉ tải qua URL ký sẵn ngắn hạn,
/// không đi qua Collaboration Service.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Lưu file; <paramref name="downloadFileName"/> là tên file khi người dùng tải về (ghi vào Content-Disposition).</summary>
    Task PutAsync(string key, Stream content, string contentType, string downloadFileName, CancellationToken ct);

    /// <summary>URL GET ký sẵn, hết hạn sau <see cref="ObjectStorageOptions.PresignMinutes"/> phút.</summary>
    string GetDownloadUrl(string key);

    Task DeleteAsync(string key, CancellationToken ct);

    Task EnsureBucketAsync(CancellationToken ct);
}

public static class FileKey
{
    /// <summary><c>workspace/{workspaceId}/{fileId}/{version}</c> (PROJECT_CONTEXT §5.8).</summary>
    public static string For(Guid workspaceId, Guid fileId, int version) => $"workspace/{workspaceId}/{fileId}/{version}";
}

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _publicClient;

    public S3ObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        _client = CreateClient(_options.ServiceUrl);
        // Chữ ký URL gồm cả host, nên ký bằng địa chỉ mà client sẽ gọi.
        _publicClient = string.IsNullOrWhiteSpace(_options.PublicUrl) ? _client : CreateClient(_options.PublicUrl);
    }

    private AmazonS3Client CreateClient(string serviceUrl) => new(
        new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            UseHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
        });

    public async Task PutAsync(string key, Stream content, string contentType, string downloadFileName, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        // Tên file tiếng Việt theo RFC 5987; ghi lúc tải lên để URL tải xuống không cần tham số ghi đè header.
        request.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(downloadFileName)}";
        await _client.PutObjectAsync(request, ct);
    }

    public string GetDownloadUrl(string key) =>
        _publicClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.PresignMinutes),
            Protocol = _publicClient.Config.ServiceURL.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTPS : Protocol.HTTP,
        });

    public Task DeleteAsync(string key, CancellationToken ct) =>
        _client.DeleteObjectAsync(_options.Bucket, key, ct);

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        var buckets = await _client.ListBucketsAsync(ct);
        if (buckets.Buckets?.Any(b => b.BucketName == _options.Bucket) != true)
        {
            await _client.PutBucketAsync(_options.Bucket, ct);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        if (!ReferenceEquals(_client, _publicClient))
        {
            _publicClient.Dispose();
        }
    }
}

/// <summary>Tạo bucket lúc khởi động nếu chưa có (thay cho container khởi tạo riêng).</summary>
public sealed class EnsureBucketHostedService(IObjectStorage storage, ILogger<EnsureBucketHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await storage.EnsureBucketAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Không chặn service khởi động; lỗi sẽ lặp lại rõ ràng khi tải file.
            logger.LogWarning(ex, "Chưa tạo / kiểm tra được bucket lưu file");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
