using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Checkpoints;
using Collab.Repositories.Workspaces;
using Collab.Services.Workspaces;

namespace Collab.Services.Checkpoints;

public interface ICheckpointService
{
    Task<CheckpointProgress> GetAsync(Guid workspaceId, CancellationToken ct);
    Task<CheckpointProgress> MarkAsync(Guid workspaceId, int checkpointNo, MarkCheckpointRequest request, WorkspaceAccess access, CancellationToken ct);
    Task UndoAsync(Guid workspaceId, int checkpointNo, WorkspaceAccess access, CancellationToken ct);
    Task<IReadOnlyList<CheckpointProgress>> MineAsync(Guid lecturerId, int? missing, CancellationToken ct);
}

public sealed class CheckpointService(
    ICheckpointRepository reviews,
    IAcademicSemesterClient academic,
    TimeProvider clock) : ICheckpointService
{
    public async Task<CheckpointProgress> GetAsync(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await reviews.WorkspaceAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        RequireExe101(workspace);
        var semesterConfig = await academic.GetConfigAsync(workspace.SemesterId, ct);
        var rows = await reviews.ReviewsAsync(workspaceId, ct);
        return Progress(workspace, rows, semesterConfig);
    }

    public async Task<CheckpointProgress> MarkAsync(Guid workspaceId, int checkpointNo,
        MarkCheckpointRequest request, WorkspaceAccess access, CancellationToken ct)
    {
        RequireCheckpointNo(checkpointNo);
        RequireInstructor(access);
        var workspace = await reviews.WorkspaceAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        RequireExe101(workspace);

        var today = Today();
        if (request.PresentedOn == default || request.PresentedOn > today)
            throw Invalid("presentedOn", "Ngày trình bày không được ở tương lai.");
        if (request.Comment?.Length > 500)
            throw Invalid("comment", "Nhận xét tối đa 500 ký tự.");
        if (await reviews.ReviewAsync(workspaceId, (short)checkpointNo, ct) is not null)
            throw new ConflictException("checkpoint-already-reviewed", "Checkpoint đã được ghi nhận");

        var instructorName = await reviews.InstructorNameAsync(workspaceId, access.PersonId, ct);
        if (instructorName is null)
            throw new ForbiddenException("not-workspace-instructor", "Chỉ giảng viên hướng dẫn được ghi nhận Checkpoint");

        reviews.Add(new CheckpointReview
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            CheckpointNo = (short)checkpointNo,
            PresentedOn = request.PresentedOn,
            Comment = request.Comment?.Trim(),
            ReviewedByLecturerId = access.PersonId,
            ReviewedByName = instructorName,
            CreatedAt = clock.GetUtcNow(),
        });

        var memberIds = await reviews.StudentIdsAsync(workspaceId, ct);
        reviews.Context.AddOutboxMessage(Topics.Collab, "CheckpointReviewed", "Workspace", workspaceId,
            new
            {
                workspaceId,
                teamId = workspace.TeamId,
                checkpointNo,
                presentedOn = request.PresentedOn,
                comment = request.Comment?.Trim(),
                memberStudentIds = memberIds
            });

        await reviews.SaveAsync(ct);
        return await GetAsync(workspaceId, ct);
    }

    public async Task UndoAsync(Guid workspaceId, int checkpointNo, WorkspaceAccess access, CancellationToken ct)
    {
        RequireCheckpointNo(checkpointNo);
        RequireInstructor(access);
        var review = await reviews.ReviewAsync(workspaceId, (short)checkpointNo, ct)
            ?? throw new NotFoundException("checkpoint", checkpointNo);

        var today = Today();
        if (review.ReviewedByLecturerId != access.PersonId || LocalDate(review.CreatedAt) != today)
            throw new ConflictException("checkpoint-locked", "Chỉ bỏ đánh dấu trong ngày đã ghi nhận");

        reviews.Remove(review);
        await reviews.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<CheckpointProgress>> MineAsync(Guid lecturerId, int? missing, CancellationToken ct)
    {
        if (missing is { } number) RequireCheckpointNo(number);
        var workspaces = await reviews.InstructorWorkspacesAsync(lecturerId, ct);
        if (workspaces.Count == 0) return [];

        var workspaceIds = workspaces.Select(x => x.Id).ToArray();
        var allReviews = await reviews.ReviewsForAsync(workspaceIds, ct);

        var semesterConfigs = new Dictionary<Guid, SemesterCheckpointConfig?>();
        foreach (var semesterId in workspaces.Select(w => w.SemesterId).Distinct())
        {
            semesterConfigs[semesterId] = await academic.GetConfigAsync(semesterId, ct);
        }

        return workspaces
            .Select(workspace =>
            {
                var rows = allReviews.Where(x => x.WorkspaceId == workspace.Id);
                var config = semesterConfigs.GetValueOrDefault(workspace.SemesterId);
                return Progress(workspace, rows, config);
            })
            .Where(x => missing is null || !x.Items[missing.Value - 1].Presented)
            .ToList();
    }

    private CheckpointProgress Progress(Workspace workspace, IEnumerable<CheckpointReview> rows, SemesterCheckpointConfig? config)
    {
        var byNumber = rows.ToDictionary(x => x.CheckpointNo);
        var today = Today();

        int currentWeek = 0;
        if (config is not null)
        {
            int daysDiff = today.DayNumber - config.StartDate.DayNumber;
            currentWeek = daysDiff < 0 ? 0 : (daysDiff / 7) + 1;
        }

        var items = Enumerable.Range(1, 4).Select(number =>
        {
            byNumber.TryGetValue((short)number, out var row);
            bool presented = row is not null;
            DateOnly? presentedOn = row?.PresentedOn;
            string? comment = row?.Comment;

            int? targetWeek = null;
            if (config?.CheckpointTargetWeeks is { Count: >= 4 } targets)
            {
                targetWeek = targets[number - 1];
            }

            bool overdue = !presented && targetWeek.HasValue && currentWeek > targetWeek.Value;
            bool canUndo = presented && LocalDate(row!.CreatedAt) == today;

            return new CheckpointItem(
                (short)number,
                presented,
                presentedOn,
                comment,
                targetWeek,
                overdue,
                canUndo);
        }).ToArray();

        return new CheckpointProgress(workspace.Id, workspace.TeamName, byNumber.Count, items);
    }

    private DateOnly Today() => LocalDate(clock.GetUtcNow());
    private static DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(value.UtcDateTime);

    private static void RequireCheckpointNo(int checkpointNo)
    {
        if (checkpointNo is < 1 or > 4) throw Invalid("checkpointNo", "Checkpoint phải từ 1 đến 4.");
    }

    private static void RequireInstructor(WorkspaceAccess access)
    {
        if (!access.IsInstructor || access.PersonType != PersonTypes.Lecturer)
            throw new ForbiddenException("not-workspace-instructor", "Chỉ giảng viên hướng dẫn được ghi nhận Checkpoint");
    }

    private static void RequireExe101(Workspace workspace)
    {
        if (workspace.CourseCode != "EXE101")
            throw new ConflictException("checkpoint-exe101-only", "Checkpoint chỉ dành cho nhóm EXE101");
    }

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
