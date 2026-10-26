// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Posts;

/// <summary>Bảng <c>team.recruitment_post_specializations</c>.</summary>
public class RecruitmentPostSpecialization
{
    public Guid RecruitmentPostId { get; set; }
    public int SpecializationId { get; set; }
}
