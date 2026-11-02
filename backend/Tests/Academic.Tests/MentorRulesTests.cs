using Academic.Services.Lecturers;
using Academic.Services.Mentors;
using BuildingBlocks.Errors;

namespace Academic.Tests;

/// <summary>Kiểm tra dữ liệu mentor (BE-66), không cần DB.</summary>
public sealed class MentorRulesTests
{
    private static readonly MentorInput Valid = new("Lê Văn Tuấn", "tuan@example.com", "Startup X", false, null, [1, 2], 4, null);

    [Fact]
    public void Valid_input_passes() => Assert.True(new MentorInputValidator().Validate(Valid).IsValid);

    [Theory]
    [InlineData("", "tuan@example.com", 4, "FullName")]
    [InlineData("Tuấn", "tuan@example.com", null, "MaxTeams")]
    [InlineData("Tuấn", "tuan@example.com", 0, "MaxTeams")]
    [InlineData("Tuấn", "tuan@example.com", 51, "MaxTeams")]
    public void Invalid_input_is_rejected(string name, string email, int? maxTeams, string field)
    {
        var result = new MentorInputValidator().Validate(Valid with { FullName = name, Email = email, MaxTeams = maxTeams });
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Email_is_left_to_the_service_which_knows_the_lecturer_link() =>
        Assert.True(new MentorInputValidator().Validate(Valid with { Email = null }).IsValid);

    [Fact]
    public void Mentor_without_lecturer_needs_a_valid_email()
    {
        Assert.Equal("tuan@example.com", MentorRules.RequireEmail("  tuan@example.com "));
        foreach (var email in new[] { null, "", "  ", "khong-phai-email", new string('a', 250) + "@a.vn" })
        {
            Assert.Contains("email", Assert.Throws<RequestValidationException>(() => MentorRules.RequireEmail(email)).Errors.Keys);
        }
    }

    [Fact]
    public void Max_teams_accepts_both_limits()
    {
        Assert.True(new MentorInputValidator().Validate(Valid with { MaxTeams = MentorRules.MinTeams }).IsValid);
        Assert.True(new MentorInputValidator().Validate(Valid with { MaxTeams = MentorRules.MaxTeams }).IsValid);
    }

    [Fact]
    public void Too_many_domains_are_rejected()
    {
        var ids = Enumerable.Range(1, LecturerRules.MaxDomains + 1).ToList();
        Assert.False(new MentorInputValidator().Validate(Valid with { DomainIds = ids }).IsValid);
        Assert.False(new MentorProfileInputValidator().Validate(new MentorProfileInput(null, null, ids)).IsValid);
    }

    [Fact]
    public void Profile_fields_are_all_optional_but_checked_when_sent()
    {
        var validator = new MentorProfileInputValidator();
        Assert.True(validator.Validate(new MentorProfileInput(null, null, null)).IsValid);
        Assert.False(validator.Validate(new MentorProfileInput(0, null, null)).IsValid);
        Assert.False(validator.Validate(new MentorProfileInput(null, new string('a', MentorRules.MaxDescription + 1), null)).IsValid);
    }

    [Theory]
    [InlineData(null, 3, null)]
    [InlineData(2, 3, 2)]
    [InlineData(3, 3, 3)]
    [InlineData(5, 3, 3)]
    public void Preferred_teams_follow_a_lowered_maximum(int? preferred, int max, int? expected) =>
        Assert.Equal((short?)expected, MentorRules.CapPreferred((short?)preferred, (short)max));

    [Fact]
    public void Query_limits_paging()
    {
        var validator = new MentorQueryValidator();
        Assert.True(validator.Validate(new MentorQuery(null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new MentorQuery(null, null, null, null, Page: 0)).IsValid);
        Assert.False(validator.Validate(new MentorQuery(null, null, null, null, PageSize: 101)).IsValid);
    }
}
