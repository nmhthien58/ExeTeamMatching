using Academic.Services.Students;
using BuildingBlocks.Errors;

namespace Academic.Tests;

/// <summary>Quy tắc hồ sơ 5 kỹ năng (BE-19), không cần DB.</summary>
public sealed class StudentProfileRulesTests
{
    private static IReadOnlyList<SkillRating> AllSkills(int level = 3) => SkillNames.All.Reverse().Select(s => new SkillRating(s, level)).ToList();

    [Fact]
    public void Five_distinct_skills_are_accepted_and_ordered()
    {
        var result = StudentProfileRules.CompleteSkills(AllSkills());
        Assert.Equal(SkillNames.All, result.Select(r => r.Skill));
    }

    [Fact]
    public void Missing_or_duplicated_skill_is_skills_incomplete()
    {
        var missing = Assert.Throws<HttpStatusException>(() => StudentProfileRules.CompleteSkills(AllSkills().Take(4).ToList()));
        Assert.Equal((400, "skills-incomplete"), (missing.Status, missing.Code));

        var duplicated = AllSkills().Take(4).Append(new SkillRating("MARKETING", 5)).ToList(); // thiếu TECHNOLOGY, trùng MARKETING
        Assert.Throws<HttpStatusException>(() => StudentProfileRules.CompleteSkills(duplicated));
    }

    [Theory]
    [InlineData("DESIGN", 3, "Skills[0].Skill")]
    [InlineData("TECHNOLOGY", 0, "Skills[0].Level")]
    [InlineData("TECHNOLOGY", 6, "Skills[0].Level")]
    public void Skill_name_and_level_are_validated(string skill, int level, string property)
    {
        var result = new StudentProfileInputValidator().Validate(new StudentProfileInput([new SkillRating(skill, level)], null, null));
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Description_is_at_most_500_characters_and_skills_are_required()
    {
        var validator = new StudentProfileInputValidator();
        Assert.True(validator.Validate(new StudentProfileInput(AllSkills(), new string('a', 500), true)).IsValid);
        Assert.False(validator.Validate(new StudentProfileInput(AllSkills(), new string('a', 501), true)).IsValid);
        Assert.False(validator.Validate(new StudentProfileInput(null, null, null)).IsValid);
    }
}
