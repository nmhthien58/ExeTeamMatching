namespace BuildingBlocks.Messaging;

/// <summary>Cấu hình Kafka (section <c>Kafka</c>).</summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Trong Docker: <c>kafka:29092</c>; chạy ngoài Docker: <c>localhost:9092</c>.</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Tên service, ghi vào <c>producer</c> của envelope và làm consumer group mặc định.</summary>
    public string ServiceName { get; set; } = "unknown";

    /// <summary>Consumer group; để trống thì dùng <see cref="ServiceName"/>.</summary>
    public string? GroupId { get; set; }

    /// <summary>Số lần thử xử lý một message trước khi đẩy sang <c>&lt;topic&gt;.dlq</c>.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Số lần thử với lỗi <see cref="IRetryableEventException"/> (chờ tăng dần, tối đa 10 giây mỗi lần ≈ 2 phút).</summary>
    public int RetryableMaxAttempts { get; set; } = 15;

    /// <summary>Thời gian chờ giữa hai lần thử (nhân theo bình phương số lần, tối đa 10 giây).</summary>
    public int RetryDelayMs { get; set; } = 500;
}
