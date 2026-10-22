using BuildingBlocks.Errors;
using ExeTeamMatching.Protos.Matching.V1;
using AdvisorKind = Matching.Services.ReadModel.AdvisorKind;
using Courses = Matching.Services.ReadModel.Courses;
using ProtoAdvisorKind = ExeTeamMatching.Protos.Matching.V1.AdvisorKind;
using ProtoSkill = ExeTeamMatching.Protos.Matching.V1.Skill;
using Skill = Matching.Services.ReadModel.Skill;
using StudentView = Matching.Services.ReadModel.StudentView;

namespace Matching.Grpc.Services;

/// <summary>Đổi giữa message proto và model của Matching.Services; dữ liệu sai → 400 (gRPC InvalidArgument).</summary>
internal static class ProtoMapping
{
    public static Guid ParseId(string value, string field) =>
        Guid.TryParse(value, out var id)
            ? id
            : throw new RequestValidationException(new Dictionary<string, string[]> { [field] = ["Phải là uuid."] });

    /// <summary>Môn của request; <c>COURSE_UNSPECIFIED</c> trả null (cả hai môn).</summary>
    public static string? ToCourseCode(Course course) => course switch
    {
        Course.Exe101 => Courses.Exe101,
        Course.Exe201 => Courses.Exe201,
        _ => null,
    };

    public static string RequireCourseCode(Course course) =>
        ToCourseCode(course) ?? throw new RequestValidationException(new Dictionary<string, string[]> { ["course"] = ["Bắt buộc chọn EXE101 hoặc EXE201."] });

    /// <summary><c>ADVISOR_KIND_UNSPECIFIED</c> = giảng viên (tương thích bản cũ của proto).</summary>
    public static AdvisorKind ToAdvisorKind(ProtoAdvisorKind kind) =>
        kind == ProtoAdvisorKind.Mentor ? AdvisorKind.Mentor : AdvisorKind.Lecturer;

    public static ProtoSkill ToProto(Skill skill) => (ProtoSkill)(int)skill;

    public static IEnumerable<SkillRating> ToProtoRatings(StudentView student) =>
        student.Skills.OrderBy(s => s.Key).Select(s => new SkillRating { Skill = ToProto(s.Key), Level = s.Value });
}
