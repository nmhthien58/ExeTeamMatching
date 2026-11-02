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
    public void Topic_interest_without_values_still_reads_as_vietnamese()
    {
        Assert.Equal("Cố vấn quan tâm đề tài của nhóm", AuditLogService.Summary(Entry("TOPIC_INTEREST_OPENED")));
        Assert.Equal("Rút quan tâm đề tài", AuditLogService.Summary(Entry("TOPIC_INTEREST_WITHDRAWN")));
    }
}
