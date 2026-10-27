// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Mentors;

/// <summary>Bảng <c>academic.mentor_domains</c> – lĩnh vực của mentor.</summary>
public class MentorDomain
{
    public Guid MentorId { get; set; }
    public int DomainId { get; set; }
}
