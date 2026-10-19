namespace BuildingBlocks.Auth;

/// <summary>
/// Cấu hình JWT (section <c>Jwt</c>). Identity ký token bằng <see cref="SigningKey"/> (HS256);
/// Gateway và các service dùng cùng khóa để kiểm tra. Khóa thật chỉ đặt qua biến môi trường <c>Jwt__SigningKey</c>.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "exe-identity";
    public string Audience { get; set; } = "exe-team-matching";

    /// <summary>Ít nhất 32 ký tự.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
