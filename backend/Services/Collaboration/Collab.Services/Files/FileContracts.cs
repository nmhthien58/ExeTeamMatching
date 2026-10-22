using FluentValidation;

namespace Collab.Services.Files;

/// <summary>Schema <c>Folder</c>. <see cref="IsArchive"/> = "EXE101 (kỳ trước)" và nội dung bên trong – chỉ đọc.</summary>
public sealed record FolderResponse(Guid Id, Guid? ParentId, string Name, bool IsArchive, int ItemCount);

/// <summary>Schema <c>ProjectFile</c> – thông tin bản mới nhất.</summary>
public sealed record ProjectFileResponse(
    Guid Id,
    Guid? FolderId,
    string Name,
    int CurrentVersion,
    long SizeBytes,
    string ContentType,
    string UpdatedByName,
    DateTimeOffset UpdatedAt,
    bool IsArchive);

/// <summary>Một phiên bản trong <c>ProjectFileDetail.versions</c>. <see cref="Id"/> là <c>fileVersionId</c> để nộp bài (C05) –
/// đề xuất bổ sung vào collab.yaml.</summary>
public sealed record FileVersionResponse(Guid Id, int Version, long SizeBytes, string UploadedByName, DateTimeOffset CreatedAt);

/// <summary>Schema <c>Comment</c> (dùng chung cho bình luận file và việc).</summary>
public sealed record CommentResponse(Guid Id, PersonRef Author, string Body, int? Version, DateTimeOffset CreatedAt);

/// <summary>Schema <c>ProjectFileDetail</c>.</summary>
public sealed record ProjectFileDetailResponse(
    Guid Id,
    Guid? FolderId,
    string Name,
    int CurrentVersion,
    long SizeBytes,
    string ContentType,
    string UpdatedByName,
    DateTimeOffset UpdatedAt,
    bool IsArchive,
    IReadOnlyList<FileVersionResponse> Versions,
    IReadOnlyList<CommentResponse> Comments);

/// <summary>Schema <c>FolderContent</c> (C06). <see cref="Folder"/> null = thư mục gốc.</summary>
public sealed record FolderContentResponse(
    FolderResponse? Folder,
    IReadOnlyList<FolderResponse> Breadcrumb,
    IReadOnlyList<FolderResponse> Folders,
    IReadOnlyList<ProjectFileResponse> Files,
    bool CanUpload);

public sealed record UploadFileResponse(ProjectFileResponse File, bool CreatedNewVersion);

public sealed record DownloadUrlResponse(string Url, DateTimeOffset ExpiresAt);

/// <summary>Body của <c>POST /api/workspaces/{id}/folders</c>.</summary>
public sealed record CreateFolderRequest(string Name, Guid? ParentId);

/// <summary>Body của <c>POST /api/files/{id}/comments</c>; <see cref="Version"/> = phiên bản được bình luận.</summary>
public sealed record AddCommentRequest(string Body, int? Version);

/// <summary>File tải lên đã đọc từ multipart (Services không phụ thuộc IFormFile).</summary>
public sealed record UploadFileCommand(Guid? FolderId, string FileName, long SizeBytes, Stream Content);

public sealed class CreateFolderRequestValidator : AbstractValidator<CreateFolderRequest>
{
    public CreateFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Nhập tên thư mục.")
            .MaximumLength(100).WithMessage("Tên thư mục tối đa 100 ký tự.")
            .Must(n => n is null || n.IndexOfAny(['/', '\\']) < 0).WithMessage("Tên thư mục không chứa / hoặc \\.");
    }
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Nhập nội dung bình luận.")
            .MaximumLength(2000).WithMessage("Bình luận tối đa 2.000 ký tự.");
        RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version is not null).WithMessage("Phiên bản không hợp lệ.");
    }
}
