using BuildingBlocks.Errors;
using Matching.Services.Composition;

namespace Matching.Tests;

/// <summary>BE-37 – ValidateComposition (rule 1, 2).</summary>
public sealed class CompositionValidatorTests
{
    private static CompositionMember[] Members(params int[] specializations) =>
        specializations.Select(s => new CompositionMember(Guid.NewGuid(), s)).ToArray();

    [Fact]
    public void Four_to_six_members_with_two_specializations_is_valid()
    {
        var result = CompositionValidator.Validate(Members(1, 1, 5, 7), CompositionRules.Default);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.MemberCount);
        Assert.Equal(3, result.SpecializationCount);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Returns_every_violation_at_once()
    {
        var result = CompositionValidator.Validate(Members(1, 1, 1), CompositionRules.Default);

        Assert.False(result.IsValid);
        Assert.Equal(
            [CompositionViolationCodes.TooFewMembers, CompositionViolationCodes.NotEnoughSpecializations],
            result.Violations.Select(v => v.Code));
    }

    [Fact]
    public void Seven_members_is_too_many()
    {
        var result = CompositionValidator.Validate(Members(1, 2, 3, 4, 5, 6, 7), CompositionRules.Default);

        Assert.Equal(CompositionViolationCodes.TooManyMembers, Assert.Single(result.Violations).Code);
    }

    [Fact]
    public void Duplicate_students_and_unknown_specialization_are_not_counted()
    {
        var a = new CompositionMember(Guid.NewGuid(), 1);
        var result = CompositionValidator.Validate([a, a, new(Guid.NewGuid(), 0), new(Guid.NewGuid(), 1), new(Guid.NewGuid(), 1)],
            CompositionRules.Default);

        Assert.Equal(4, result.MemberCount);
        Assert.Equal(1, result.SpecializationCount);
    }

    [Fact]
    public void Zero_in_request_means_default_rules()
    {
        Assert.Equal(CompositionRules.Default, CompositionRules.From(0, 0, 0));
        Assert.Equal(new CompositionRules(3, 5, 1), CompositionRules.From(3, 5, 1));
    }

    [Fact]
    public void Min_greater_than_max_is_rejected()
    {
        var error = Assert.Throws<RequestValidationException>(() => CompositionRules.From(7, 6, 2));

        Assert.Contains("minSize", error.Errors.Keys);
    }
}
