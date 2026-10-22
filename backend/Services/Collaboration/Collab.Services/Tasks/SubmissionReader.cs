using Collab.Repositories.Files;
using Collab.Repositories.Tasks;
using Collab.Services.Files;

namespace Collab.Services.Tasks;

/// <summary>Đọc bản nộp kèm file và phiên bản đã nộp (dùng chung cho chi tiết việc và thao tác nộp / duyệt).</summary>
public sealed class SubmissionReader(IFileRepository files)
{
    public async Task<IReadOnlyList<SubmissionResponse>> MapAsync(IReadOnlyList<DeliverableSubmission> submissions,
        WorkspacePeople people, DateTimeOffset? dueAt, bool isArchive, CancellationToken ct)
    {
        if (submissions.Count == 0)
        {
            return [];
        }

        var versions = (await files.GetVersionsByIdsAsync(submissions.Select(s => s.FileVersionId).Distinct().ToList(), ct))
            .ToDictionary(v => v.Id);
        var fileRows = (await files.ListWithLatestAsync(versions.Values.Select(v => v.FileId).Distinct().ToList(), ct))
            .ToDictionary(f => f.File.Id);

        return submissions
            .Select(s =>
            {
                var version = versions[s.FileVersionId];
                return TaskMapper.ToSubmission(s, version, fileRows[version.FileId], people, dueAt, isArchive);
            })
            .ToList();
    }
}
