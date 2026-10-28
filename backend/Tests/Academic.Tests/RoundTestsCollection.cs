namespace Academic.Tests;

/// <summary>
/// Lớp test mở đợt đăng ký chạy tuần tự với nhau: mỗi môn + loại chỉ một đợt mở một lúc (round-already-open),
/// nên hai lớp chạy song song có thể giành cùng một cặp môn / loại.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RoundTestsCollection
{
    public const string Name = "registration-rounds";
}
