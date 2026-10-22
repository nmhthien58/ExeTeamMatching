using BuildingBlocks.Errors;

namespace Collab.Services.Tasks;

/// <summary>Lỗi của module Tasks (mã lỗi ở docs/contracts/README.md và collab.yaml).</summary>
public static class TaskErrors
{
    public static NotFoundException TaskNotFound(Guid taskId) => new("việc", taskId);

    public static ForbiddenException InstructorOnly(string title) => new("instructor-only", title);

    public static ForbiddenException StudentsOnly(string title) => new(title: title);

    public static ConflictException NotDeliverable() =>
        new("not-deliverable", "Việc này không cần nộp bài", "Chỉ nộp file cho việc loại bài nộp.");

    public static ConflictException DeliverableHasSubmissions() =>
        new("deliverable-has-submissions", "Bài nộp đã có bản nộp", "Không xóa được bài nộp mà nhóm đã nộp file.");

    public static ConflictException SubmissionReviewed() =>
        new("submission-reviewed", "Bản nộp đã được duyệt", "Nhóm cần nộp phiên bản mới để duyệt lại.");

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
