using Academic.Services.Lecturers;

namespace Academic.Tests;

/// <summary>Kiểm tra dữ liệu giảng viên (BE-20), không cần DB.</summary>
public sealed class LecturerRulesTests
{
    private static readonly LecturerInput Valid = new("Trần Thị B", "b@example.com", null, [1, 2], null, 40, null);

    [Fact]
    public void Valid_input_passes_and_max_teams_may_be_empty() =>
        Assert.True(new LecturerInputValidator().Validate(Valid).IsValid);

    [Theory]
    [InlineData("", "b@example.com", null, null, "FullName")]
    [InlineData("Trần Thị B", "", null, null, "Email")]
    [InlineData("Trần Thị B", "khong-phai-email", null, null, "Email")]
    [InlineData("Trần Thị B", "b@example.com", "12345", null, "Phone")]
    [InlineData("Trần Thị B", "b@example.com", null, 201, "MaxTeamsExe101")]
    [InlineData("Trần Thị B", "b@example.com", null, -1, "MaxTeamsExe101")]
    public void Invalid_input_is_rejected(string name, string email, string? phone, int? max101, string field)
    {
        var result = new LecturerInputValidator().Validate(Valid with { FullName = name, Email = email, Phone = phone, MaxTeamsExe101 = max101 });
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Too_many_domains_are_rejected()
    {
        var ids = Enumerable.Range(1, LecturerRules.MaxDomains + 1).ToList();
        Assert.False(new LecturerInputValidator().Validate(Valid with { DomainIds = ids }).IsValid);
        Assert.False(new LecturerProfileInputValidator().Validate(new LecturerProfileInput(ids)).IsValid);
        Assert.False(new LecturerProfileInputValidator().Validate(new LecturerProfileInput(null)).IsValid);
        Assert.True(new LecturerProfileInputValidator().Validate(new LecturerProfileInput([])).IsValid);
    }

    [Theory]
    [InlineData("090 123.45-67", "0901234567")]
    [InlineData("+84 (90) 1234567", "+84901234567")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Phone_is_kept_as_digits_string(string? input, string? expected) =>
        Assert.Equal(expected, LecturerRules.NormalizePhone(input));
}
