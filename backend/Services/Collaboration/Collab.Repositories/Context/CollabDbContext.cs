using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Context;

/// <summary>
/// DbContext của <c>collab_db</c> (schema <c>collab</c>). Map đúng bảng trong deploy/db/06_collab_db.sql:
/// tên cột snake_case qua UseSnakeCaseNamingConvention, tên bảng và khóa khai báo dưới đây.
/// Chỉ Thiên sửa file này (AGENTS.md mục 2); module cần thêm bảng / quan hệ thì đề xuất qua PR.
/// </summary>
public class CollabDbContext(DbContextOptions<CollabDbContext> options) : DbContext(options)
{
    public const string Schema = "collab";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutboxMessages(Schema).AddProcessedEvents(Schema);

        modelBuilder.Entity<Workspaces.Workspace>(e => { e.ToTable("workspaces"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Workspaces.WorkspaceMember>(e => { e.ToTable("workspace_members"); e.HasKey(x => new { x.WorkspaceId, x.PersonType, x.PersonId }); e.Property(x => x.JoinedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Messages.Channel>(e => { e.ToTable("channels"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Messages.Message>(e => { e.ToTable("messages"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Messages.MessageAttachment>(e => { e.ToTable("message_attachments"); e.HasKey(x => new { x.MessageId, x.FileId }); });
        modelBuilder.Entity<Messages.MessageRead>(e => { e.ToTable("message_reads"); e.HasKey(x => new { x.ChannelId, x.PersonType, x.PersonId }); e.Property(x => x.LastReadAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Messages.AdvisorQuestion>(e => { e.ToTable("advisor_questions"); e.HasKey(x => x.MessageId); });
        modelBuilder.Entity<Files.Folder>(e => { e.ToTable("folders"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Files.ProjectFile>(e => { e.ToTable("project_files"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Files.FileVersion>(e => { e.ToTable("file_versions"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Files.FileComment>(e => { e.ToTable("file_comments"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.Meeting>(e => { e.ToTable("meetings"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.MeetingSlot>(e => { e.ToTable("meeting_slots"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Meetings.MeetingRespons>(e => { e.ToTable("meeting_responses"); e.HasKey(x => new { x.SlotId, x.PersonType, x.PersonId }); e.Property(x => x.AnsweredAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.MeetingMinutes>(e => { e.ToTable("meeting_minutes"); e.HasKey(x => x.MeetingId); e.Property(x => x.RecordedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.MeetingReminder>(e => { e.ToTable("meeting_reminders"); e.HasKey(x => new { x.MeetingId, x.OffsetMinutes }); e.Property(x => x.SentAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.MeetingSchedule>(e => { e.ToTable("meeting_schedules"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Meetings.ScheduleWindow>(e => { e.ToTable("schedule_windows"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Meetings.ScheduleSlot>(e => { e.ToTable("schedule_slots"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<Meetings.SlotBooking>(e => { e.ToTable("slot_bookings"); e.HasKey(x => x.Id); e.Property(x => x.BookedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Tasks.TaskItem>(e => { e.ToTable("task_items"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Tasks.TaskAssignee>(e => { e.ToTable("task_assignees"); e.HasKey(x => new { x.TaskId, x.StudentId }); });
        modelBuilder.Entity<Tasks.TaskComment>(e => { e.ToTable("task_comments"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Tasks.DeliverableSubmission>(e => { e.ToTable("deliverable_submissions"); e.HasKey(x => x.Id); e.Property(x => x.SubmittedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<Checkpoints.CheckpointReview>(e => { e.ToTable("checkpoint_reviews"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });

        // Khóa ngoại – để EF ghi đúng thứ tự (bảng cha trước) và khớp hành vi xóa của DB script.
        modelBuilder.Entity<Messages.AdvisorQuestion>().HasOne<Messages.Message>().WithMany().HasForeignKey(x => x.MessageId).HasConstraintName("fk_advisor_questions_message").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.AdvisorQuestion>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_advisor_questions_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.Channel>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_channels_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Checkpoints.CheckpointReview>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_checkpoint_reviews_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tasks.DeliverableSubmission>().HasOne<Files.FileVersion>().WithMany().HasForeignKey(x => x.FileVersionId).HasConstraintName("fk_deliverable_submissions_version").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Tasks.DeliverableSubmission>().HasOne<Tasks.TaskItem>().WithMany().HasForeignKey(x => x.TaskId).HasConstraintName("fk_deliverable_submissions_task").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.FileComment>().HasOne<Files.ProjectFile>().WithMany().HasForeignKey(x => x.FileId).HasConstraintName("fk_file_comments_file").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.FileComment>().HasOne<Files.FileVersion>().WithMany().HasForeignKey(x => x.FileVersionId).HasConstraintName("fk_file_comments_version").OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Files.FileVersion>().HasOne<Files.ProjectFile>().WithMany().HasForeignKey(x => x.FileId).HasConstraintName("fk_file_versions_file").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.Folder>().HasOne<Files.Folder>().WithMany().HasForeignKey(x => x.ParentId).HasConstraintName("fk_folders_parent").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.Folder>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_folders_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.MeetingMinutes>().HasOne<Meetings.Meeting>().WithMany().HasForeignKey(x => x.MeetingId).HasConstraintName("fk_meeting_minutes_meeting").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.MeetingReminder>().HasOne<Meetings.Meeting>().WithMany().HasForeignKey(x => x.MeetingId).HasConstraintName("fk_meeting_reminders_meeting").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.MeetingRespons>().HasOne<Meetings.MeetingSlot>().WithMany().HasForeignKey(x => x.SlotId).HasConstraintName("fk_meeting_responses_slot").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.MeetingSlot>().HasOne<Meetings.Meeting>().WithMany().HasForeignKey(x => x.MeetingId).HasConstraintName("fk_meeting_slots_meeting").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.Meeting>().HasOne<Meetings.MeetingSlot>().WithMany().HasForeignKey(x => x.ConfirmedSlotId).HasConstraintName("fk_meetings_confirmed_slot").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Meetings.Meeting>().HasOne<Meetings.SlotBooking>().WithMany().HasForeignKey(x => x.ScheduleBookingId).HasConstraintName("fk_meetings_schedule_booking").OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Meetings.Meeting>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_meetings_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.MessageAttachment>().HasOne<Files.ProjectFile>().WithMany().HasForeignKey(x => x.FileId).HasConstraintName("fk_message_attachments_file").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.MessageAttachment>().HasOne<Messages.Message>().WithMany().HasForeignKey(x => x.MessageId).HasConstraintName("fk_message_attachments_message").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.MessageRead>().HasOne<Messages.Channel>().WithMany().HasForeignKey(x => x.ChannelId).HasConstraintName("fk_message_reads_channel").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.MessageRead>().HasOne<Messages.Message>().WithMany().HasForeignKey(x => x.LastReadMessageId).HasConstraintName("fk_message_reads_message").OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Messages.Message>().HasOne<Messages.Channel>().WithMany().HasForeignKey(x => x.ChannelId).HasConstraintName("fk_messages_channel").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Messages.Message>().HasOne<Messages.Message>().WithMany().HasForeignKey(x => x.ParentMessageId).HasConstraintName("fk_messages_parent").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.ProjectFile>().HasOne<Files.Folder>().WithMany().HasForeignKey(x => x.FolderId).HasConstraintName("fk_project_files_folder").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Files.ProjectFile>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_project_files_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.ScheduleSlot>().HasOne<Meetings.MeetingSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).HasConstraintName("fk_schedule_slots_schedule").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.ScheduleSlot>().HasOne<Meetings.ScheduleWindow>().WithMany().HasForeignKey(x => x.WindowId).HasConstraintName("fk_schedule_slots_window").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.ScheduleWindow>().HasOne<Meetings.MeetingSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).HasConstraintName("fk_schedule_windows_schedule").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.SlotBooking>().HasOne<Meetings.MeetingSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).HasConstraintName("fk_slot_bookings_schedule").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.SlotBooking>().HasOne<Meetings.ScheduleSlot>().WithMany().HasForeignKey(x => x.SlotId).HasConstraintName("fk_slot_bookings_slot").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Meetings.SlotBooking>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_slot_bookings_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tasks.TaskAssignee>().HasOne<Tasks.TaskItem>().WithMany().HasForeignKey(x => x.TaskId).HasConstraintName("fk_task_assignees_task").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tasks.TaskComment>().HasOne<Tasks.TaskItem>().WithMany().HasForeignKey(x => x.TaskId).HasConstraintName("fk_task_comments_task").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tasks.TaskItem>().HasOne<Meetings.Meeting>().WithMany().HasForeignKey(x => x.SourceMeetingId).HasConstraintName("fk_task_items_meeting").OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Tasks.TaskItem>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_task_items_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Workspaces.WorkspaceMember>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).HasConstraintName("fk_workspace_members_workspace").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Workspaces.Workspace>().HasOne<Workspaces.Workspace>().WithMany().HasForeignKey(x => x.PreviousWorkspaceId).HasConstraintName("fk_workspaces_previous").OnDelete(DeleteBehavior.SetNull);
    }
}
