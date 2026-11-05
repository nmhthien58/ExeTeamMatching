using System.Text.Json;
using BuildingBlocks.Messaging;
using Team.Repositories.AuditLog;
using Team.Services.AuditLog;

namespace Team.Tests;

/// <summary>Câu mô tả nhật ký cho hành động của module AdvisorMatching và TopicInterests (BE-80). Giá trị ghi như AuditLogWriter.</summary>
public sealed class AuditLogSummaryTests
{
    private static AuditLogEntry Entry(string action, object? oldValues = null, object? newValues = null) => new()
    {
        Action = action,
        ActorRole = "System",
        OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, EventEnvelope.JsonOptions),
        NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, EventEnvelope.JsonOptions),
    };

    [Fact]
    public void Lecturer_assigned_by_matching_names_the_lecturer()
    {
        var e = Entry("LECTURER_ASSIGNED", newValues: new
        {
            lecturerId = Guid.NewGuid(), lecturerName = "GV An", reason = "MUTUAL", preferenceRank = 1, source = "AUTO", matchingRunId = Guid.NewGuid(),
        });

        Assert.Equal("Hệ thống ghép giảng viên hướng dẫn GV An", AuditLogService.Summary(e));
    }

    [Fact]
    public void Lecturer_assigned_manually_without_previous_lecturer()
    {
        var e = Entry("LECTURER_ASSIGNED", newValues: new
        {
            lecturerId = Guid.NewGuid(), lecturerName = "GV Bình", reason = "MANUAL", preferenceRank = (int?)null, source = "MANUAL", matchingRunId = (Guid?)null,
        });

        Assert.Equal("Gán giảng viên hướng dẫn GV Bình", AuditLogService.Summary(e));
    }

    [Fact]
    public void Lecturer_changed_names_old_and_new_lecturer()
    {
        var e = Entry("LECTURER_ASSIGNED",
            oldValues: new { lecturerId = Guid.NewGuid(), lecturerName = "GV An", reason = "MUTUAL" },
            newValues: new { lecturerId = Guid.NewGuid(), lecturerName = "GV Bình", reason = "MANUAL", source = "MANUAL" });

        Assert.Equal("Đổi giảng viên hướng dẫn từ GV An sang GV Bình", AuditLogService.Summary(e));
    }

    [Fact]
    public void Topic_interest_opened_names_the_advisor()
    {
        var e = Entry("TOPIC_INTEREST_OPENED", newValues: new
        {
            advisorKind = "LECTURER", advisorId = Guid.NewGuid(), advisorName = "GV An", wasPreviousInstructor = false,
        });

        Assert.Equal("GV An quan tâm đề tài của nhóm", AuditLogService.Summary(e));
    }

    [Fact]
    public void Mentor_assigned_changed_and_removed_name_the_mentor()
    {
        var auto = Entry("MENTOR_ASSIGNED", newValues: new { mentorId = Guid.NewGuid(), mentorName = "Chị Lan", reason = "DOMAIN", source = "AUTO" });
        var changed = Entry("MENTOR_ASSIGNED",
            oldValues: new { mentorId = Guid.NewGuid(), mentorName = "Chị Lan", reason = "DOMAIN" },
            newValues: new { mentorId = Guid.NewGuid(), mentorName = "Anh Minh", reason = "MANUAL", source = "MANUAL" });
        var removed = Entry("MENTOR_UNASSIGNED", oldValues: new { mentorId = Guid.NewGuid(), mentorName = "Anh Minh", reason = "MANUAL" });

        Assert.Equal("Hệ thống ghép mentor Chị Lan", AuditLogService.Summary(auto));
        Assert.Equal("Đổi mentor từ Chị Lan sang Anh Minh", AuditLogService.Summary(changed));
        Assert.Equal("Gỡ mentor Anh Minh", AuditLogService.Summary(removed));
    }

    /// <summary>Mọi mã hành động module khác ghi vào nhật ký nhóm đều thành câu tiếng Việt, không hiện nguyên mã (W16, W24, S22).</summary>
    [Theory]
    [InlineData("MEMBER_ADDED", "Bộ môn thêm SE180009 vào nhóm")]
    [InlineData("TEAM_FINALIZED", "Chốt nhóm")]
    [InlineData("TEAM_NEEDS_REVIEW", "Nhóm cần Bộ môn xem lại")]
    [InlineData("TEAM_REOPENED", "Mở lại nhóm")]
    [InlineData("TEAM_DISBANDED", "Giải tán nhóm")]
    [InlineData("TEAM_LINKED_TO_PROJECT", "Gắn nhóm với đề tài của kỳ trước")]
    [InlineData("CHANGE_REQUEST_SUBMITTED", "Gửi yêu cầu đổi nhóm (2 thay đổi)")]
    [InlineData("CHANGE_REQUEST_APPROVED", "Bộ môn duyệt yêu cầu đổi nhóm")]
    [InlineData("CHANGE_REQUEST_REJECTED", "Bộ môn từ chối yêu cầu đổi nhóm")]
    [InlineData("PROJECT_CHANGE_PENDING", "Đề xuất đổi đề tài thành EduSpark Pro – chờ Bộ môn duyệt")]
    [InlineData("PROJECT_CHANGE_CANCELLED", "Hủy đề xuất đổi đề tài")]
    [InlineData("PROJECT_PIVOT_APPROVED", "Đổi đề tài thành EduSpark Pro")]
    [InlineData("LECTURER_PREFERENCES_UPDATED", "Sửa nguyện vọng giảng viên")]
    [InlineData("UNMATCHED_REMINDED", "Nhắc 5 sinh viên chưa có nhóm")]
    public void Actions_of_other_modules_read_as_vietnamese(string action, string expected)
    {
        var e = Entry(action, newValues: new
        {
            studentCode = "SE180009", changedMemberCount = 2, changedPercent = 40, newProjectName = "EduSpark Pro", name = "EduSpark Pro", studentCount = 5,
        });

        Assert.Equal(expected, AuditLogService.Summary(e));
    }

    [Fact]
    public void Topic_interest_without_values_still_reads_as_vietnamese()
    {
        Assert.Equal("Cố vấn quan tâm đề tài của nhóm", AuditLogService.Summary(Entry("TOPIC_INTEREST_OPENED")));
        Assert.Equal("Rút quan tâm đề tài", AuditLogService.Summary(Entry("TOPIC_INTEREST_WITHDRAWN")));
    }
}
