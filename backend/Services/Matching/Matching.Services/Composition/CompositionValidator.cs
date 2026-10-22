using BuildingBlocks.Errors;

namespace Matching.Services.Composition;

public sealed record CompositionMember(Guid StudentId, int SpecializationId);

/// <summary>Quy tắc 1, 2: nhóm 4–6 người, ít nhất 2 chuyên ngành hẹp. Giá trị 0 trong request = dùng mặc định.</summary>
public sealed record CompositionRules(int MinSize, int MaxSize, int MinSpecializations)
{
    public const int DefaultMinSize = 4;
    public const int DefaultMaxSize = 6;
    public const int DefaultMinSpecializations = 2;

    public static readonly CompositionRules Default = new(DefaultMinSize, DefaultMaxSize, DefaultMinSpecializations);

    /// <summary>Ghép giá trị từ request (0 = mặc định) và kiểm tra hợp lệ; sai thì 400 (gRPC InvalidArgument).</summary>
    public static CompositionRules From(int minSize, int maxSize, int minSpecializations)
    {
        var rules = new CompositionRules(
            minSize > 0 ? minSize : DefaultMinSize,
            maxSize > 0 ? maxSize : DefaultMaxSize,
            minSpecializations > 0 ? minSpecializations : DefaultMinSpecializations);

        var errors = new Dictionary<string, string[]>();
        if (minSize < 0 || maxSize < 0 || minSpecializations < 0)
        {
            errors["rules"] = ["Giá trị không được âm."];
        }
        if (rules.MinSize > rules.MaxSize)
        {
            errors["minSize"] = ["Số thành viên tối thiểu lớn hơn tối đa."];
        }
        if (rules.MinSpecializations > rules.MaxSize)
        {
            errors["minSpecializations"] = ["Số chuyên ngành tối thiểu lớn hơn số thành viên tối đa."];
        }
        return errors.Count == 0 ? rules : throw new RequestValidationException(errors);
    }
}

public static class CompositionViolationCodes
{
    public const string TooFewMembers = "TOO_FEW_MEMBERS";
    public const string TooManyMembers = "TOO_MANY_MEMBERS";
    public const string NotEnoughSpecializations = "NOT_ENOUGH_SPECIALIZATIONS";
}

public sealed record CompositionViolation(string Code, string Message);

public sealed record CompositionResult(bool IsValid, int MemberCount, int SpecializationCount, IReadOnlyList<CompositionViolation> Violations);

/// <summary>ValidateComposition (BE-37): kiểm tra sĩ số và số chuyên ngành, trả mọi vi phạm cùng lúc.</summary>
public static class CompositionValidator
{
    public static CompositionResult Validate(IEnumerable<CompositionMember> members, CompositionRules rules)
    {
        var distinct = members.DistinctBy(m => m.StudentId).ToList();
        var memberCount = distinct.Count;
        var specializationCount = CountSpecializations(distinct.Select(m => m.SpecializationId));

        var violations = new List<CompositionViolation>();
        if (memberCount < rules.MinSize)
        {
            violations.Add(new(CompositionViolationCodes.TooFewMembers,
                $"Nhóm có {memberCount} thành viên, cần ít nhất {rules.MinSize}."));
        }
        if (memberCount > rules.MaxSize)
        {
            violations.Add(new(CompositionViolationCodes.TooManyMembers,
                $"Nhóm có {memberCount} thành viên, tối đa {rules.MaxSize}."));
        }
        if (specializationCount < rules.MinSpecializations)
        {
            violations.Add(new(CompositionViolationCodes.NotEnoughSpecializations,
                $"Nhóm có {specializationCount} chuyên ngành, cần ít nhất {rules.MinSpecializations}."));
        }

        return new CompositionResult(violations.Count == 0, memberCount, specializationCount, violations);
    }

    /// <summary>Số chuyên ngành khác nhau; id 0 (chưa biết chuyên ngành) không tính.</summary>
    public static int CountSpecializations(IEnumerable<int> specializationIds) =>
        specializationIds.Where(id => id > 0).Distinct().Count();

    public static bool IsValid(IEnumerable<int> specializationIds, CompositionRules rules)
    {
        var list = specializationIds.ToList();
        return list.Count >= rules.MinSize && list.Count <= rules.MaxSize && CountSpecializations(list) >= rules.MinSpecializations;
    }
}
