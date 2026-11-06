using Academic.Services.Students;

namespace Academic.Tests;

/// <summary>Kiểm tra query của <c>GET /api/students</c> (BE-18), không cần DB.</summary>
public sealed class StudentQueryValidatorTests
{
    private static readonly StudentQueryValidator Validator = new();
    private static readonly StudentQuery Query = new(null, null, null, null, null, null, null);

    [Fact]
    public void Defaults_are_valid() => Assert.True(Validator.Validate(Query).IsValid);

    [Theory]
    [InlineData("name")]
    [InlineData("-name")]
    [InlineData("studentCode")]
    [InlineData("-studentCode")]
    public void Known_sorts_are_accepted(string sort) => Assert.True(Validator.Validate(Query with { Sort = sort }).IsValid);

    [Fact]
    public void Bad_values_are_rejected()
    {
        Assert.False(Validator.Validate(Query with { Sort = "email" }).IsValid);
        Assert.False(Validator.Validate(Query with { CourseCode = "EXE301" }).IsValid);
        Assert.False(Validator.Validate(Query with { Page = 0 }).IsValid);
        Assert.False(Validator.Validate(Query with { PageSize = 101 }).IsValid);
    }
}
