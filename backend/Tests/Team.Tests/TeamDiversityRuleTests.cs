using Team.Repositories.Members;
using Team.Services.Teams;
using Xunit;

namespace Team.Tests;

public sealed class TeamDiversityRuleTests
{
    [Fact]
    public void Two_students_with_different_majors_are_diverse_even_without_specialization()
    {
        var members = new (int MajorId, int? SpecializationId)[]
        {
            (1, null), // SE, no specialization
            (2, null)  // SS, no specialization
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(2, diversity);
    }

    [Fact]
    public void Two_students_with_same_major_and_different_declared_specializations_are_diverse()
    {
        var members = new (int MajorId, int? SpecializationId)[]
        {
            (1, 10), // SE, Software Engineering
            (1, 11)  // SE, Artificial Intelligence
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(2, diversity);
    }

    [Fact]
    public void Two_students_with_same_major_and_no_specialization_are_not_diverse()
    {
        var members = new (int MajorId, int? SpecializationId)[]
        {
            (1, null),
            (1, null)
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(1, diversity);
    }

    [Fact]
    public void Two_students_with_same_major_and_same_specialization_are_not_diverse()
    {
        var members = new (int MajorId, int? SpecializationId)[]
        {
            (1, 10),
            (1, 10)
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(1, diversity);
    }

    [Fact]
    public void Two_students_with_same_major_one_declared_and_one_undeclared_are_diverse()
    {
        var members = new (int MajorId, int? SpecializationId)[]
        {
            (1, 10), // SE with declared spec
            (1, null) // SE general
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(2, diversity);
    }

    [Fact]
    public void Team_member_entities_support_diversity_count()
    {
        var members = new List<TeamMember>
        {
            new() { MajorId = 1, SpecializationId = 10 },
            new() { MajorId = 1, SpecializationId = 10 },
            new() { MajorId = 2, SpecializationId = null },
            new() { MajorId = 2, SpecializationId = null }
        };

        var diversity = TeamDiversityRule.CountDiversity(members);
        Assert.Equal(2, diversity);
    }
}
