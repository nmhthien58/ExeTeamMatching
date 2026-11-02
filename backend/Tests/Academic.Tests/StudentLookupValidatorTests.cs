using Academic.Services.Students;

namespace Academic.Tests;

/// <summary>Kiểm tra body của <c>POST /internal/students/lookup</c> (BE-22), không cần DB.</summary>
public sealed class StudentLookupValidatorTests
{
    private static readonly StudentLookupRequestValidator Validator = new();

    [Fact]
    public void Missing_student_ids_is_rejected()
    {
        var result = Validator.Validate(new StudentLookupRequest(Guid.NewGuid(), null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StudentLookupRequest.StudentIds) && e.ErrorMessage == "Thiếu studentIds.");
    }

    [Fact]
    public void Empty_list_is_valid_but_too_many_ids_are_not()
    {
        Assert.True(Validator.Validate(new StudentLookupRequest(Guid.NewGuid(), [])).IsValid);
        var ids = Enumerable.Range(0, StudentLookupRequestValidator.MaxIds + 1).Select(_ => Guid.NewGuid()).ToList();
        Assert.False(Validator.Validate(new StudentLookupRequest(Guid.NewGuid(), ids)).IsValid);
        Assert.True(Validator.Validate(new StudentLookupRequest(Guid.NewGuid(), ids.Take(StudentLookupRequestValidator.MaxIds).ToList())).IsValid);
    }
}
