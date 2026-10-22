using BuildingBlocks.Errors;

namespace Collab.Services.Files;

/// <summary>Quy tắc file dự án (rule 24): tối đa 25 MB; pdf, docx, pptx, xlsx, png, jpg, zip.</summary>
public static class FileRules
{
    /// <summary>25 MB – khớp <c>ck_file_versions_size</c> trong 06_collab_db.sql.</summary>
    public const long MaxSizeBytes = 25 * 1024 * 1024;

    public const int MaxNameLength = 255;

    /// <summary>Kiểu nội dung lấy theo đuôi file (không tin header của client).</summary>
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".zip"] = "application/zip",
    };

    /// <summary>Kiểm tra tên, đuôi, kích thước; trả tên đã chuẩn hóa và kiểu nội dung.</summary>
    public static (string Name, string ContentType) Validate(string fileName, long sizeBytes)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength || name.Any(char.IsControl))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"Tên file phải có 1–{MaxNameLength} ký tự hợp lệ."],
            });
        }
        if (!ContentTypes.TryGetValue(Path.GetExtension(name), out var contentType))
        {
            throw new HttpStatusException(415, "file-type-not-allowed", "Định dạng file không được hỗ trợ",
                "Chỉ nhận pdf, docx, pptx, xlsx, png, jpg, zip.");
        }
        if (sizeBytes <= 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = ["File rỗng."] });
        }
        if (sizeBytes > MaxSizeBytes)
        {
            throw new HttpStatusException(413, "file-too-large", "File quá lớn", "Mỗi file tối đa 25 MB.");
        }
        return (name, contentType);
    }
}
