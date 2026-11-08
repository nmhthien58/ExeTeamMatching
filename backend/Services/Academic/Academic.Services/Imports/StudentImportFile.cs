using System.Globalization;
using System.Text;
using BuildingBlocks.Errors;
using ClosedXML.Excel;

namespace Academic.Services.Imports;

/// <summary>
/// File Excel danh sách sinh viên (ClosedXML): đọc file Admin tải lên và tạo file mẫu.
/// Sheet đầu tiên, dòng 1 là tiêu đề; cột nhận theo tên tiêu đề (không phân biệt hoa thường, dấu) nên đổi thứ tự cột vẫn đọc được.
/// </summary>
public static class StudentImportFile
{
    /// <summary>Số dòng dữ liệu tối đa mỗi file.</summary>
    public const int MaxRows = 3000;

    /// <summary>Tiêu đề trong file mẫu, theo thứ tự cột.</summary>
    public static readonly IReadOnlyList<string> Headers = ["MSSV", "Họ tên", "Email", "Mã lớp", "SĐT"];

    // Tên tiêu đề nhận được (đã bỏ dấu, khoảng trắng, viết thường) → trường của ImportRow.
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["mssv"] = ImportFields.StudentCode, ["masinhvien"] = ImportFields.StudentCode, ["studentcode"] = ImportFields.StudentCode, ["rollnumber"] = ImportFields.StudentCode,
        ["hoten"] = ImportFields.FullName, ["hovaten"] = ImportFields.FullName, ["fullname"] = ImportFields.FullName,
        ["email"] = ImportFields.Email,
        ["chuyennganh"] = ImportFields.SpecializationCode, ["machuyennganh"] = ImportFields.SpecializationCode, ["specialization"] = ImportFields.SpecializationCode, ["specializationcode"] = ImportFields.SpecializationCode,
        ["malop"] = ImportFields.ClassCode, ["lop"] = ImportFields.ClassCode, ["classcode"] = ImportFields.ClassCode, ["class"] = ImportFields.ClassCode,
        ["sdt"] = ImportFields.Phone, ["sodienthoai"] = ImportFields.Phone, ["dienthoai"] = ImportFields.Phone, ["phone"] = ImportFields.Phone,
    };

    private static readonly string[] RequiredFields = [ImportFields.StudentCode, ImportFields.FullName, ImportFields.Email, ImportFields.ClassCode];

    /// <summary>
    /// Đọc các dòng dữ liệu, bỏ dòng trống. Giá trị là chuỗi đã bỏ khoảng trắng hai đầu; ô số (SĐT gõ dạng số) đọc thành chuỗi chữ số.
    /// File hỏng, thiếu cột bắt buộc, không có dòng nào hoặc quá <see cref="MaxRows"/> dòng → 400 ở field <c>file</c>.
    /// </summary>
    public static IReadOnlyList<ImportRow> Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw FileError("Không đọc được file. Hãy dùng file .xlsx theo mẫu.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw FileError("File không có sheet nào.");
            var columns = MapHeader(sheet.Row(1));
            var missing = RequiredFields.Where(f => !columns.ContainsKey(f)).ToList();
            if (missing.Count > 0)
            {
                throw FileError($"Thiếu cột: {string.Join(", ", missing.Select(f => Headers[Array.IndexOf(ImportFields.All, f)]))}. Tải file mẫu để xem đúng tiêu đề.");
            }

            var rows = new List<ImportRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                string Cell(string field) => columns.TryGetValue(field, out var c) ? CellText(row.Cell(c)) : string.Empty;
                var phone = Cell(ImportFields.Phone);
                var item = new ImportRow(r, Cell(ImportFields.StudentCode), Cell(ImportFields.FullName), Cell(ImportFields.Email),
                    Cell(ImportFields.SpecializationCode), Cell(ImportFields.ClassCode), phone.Length == 0 ? null : phone);
                if (item is { StudentCode: "", FullName: "", Email: "", SpecializationCode: "", ClassCode: "", Phone: null })
                {
                    continue;
                }
                rows.Add(item);
                if (rows.Count > MaxRows)
                {
                    throw FileError($"File tối đa {MaxRows} sinh viên.");
                }
            }
            return rows.Count > 0 ? rows : throw FileError("File không có dòng dữ liệu nào.");
        }
    }

    /// <summary>File mẫu: dòng tiêu đề, một dòng ví dụ, cột MSSV và SĐT định dạng chữ để Excel không bỏ số 0 đầu.</summary>
    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("SinhVien");
        for (var i = 0; i < Headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(5).Style.NumberFormat.Format = "@";
        string[] example = ["SE184118", "Nguyễn Văn A", "a.nguyen@example.com", "SE1950", "0901234567"];
        for (var i = 0; i < example.Length; i++)
        {
            sheet.Cell(2, i + 1).Value = example[i];
        }
        sheet.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static Dictionary<string, int> MapHeader(IXLRow header)
    {
        var columns = new Dictionary<string, int>();
        foreach (var cell in header.CellsUsed())
        {
            if (Aliases.TryGetValue(Normalize(CellText(cell)), out var field))
            {
                columns.TryAdd(field, cell.Address.ColumnNumber);
            }
        }
        return columns;
    }

    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsNumber)
        {
            return value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture);
        }
        return value.IsBlank ? string.Empty : cell.GetString().Trim();
    }

    /// <summary>"Họ tên" → "hoten", "SĐT" → "sdt".</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || !char.IsLetterOrDigit(c))
            {
                continue;
            }
            builder.Append(c is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static RequestValidationException FileError(string message) => new(new Dictionary<string, string[]> { ["file"] = [message] });
}

/// <summary>Tên trường của <see cref="ImportRow"/> (camelCase như JSON), dùng làm <c>field</c> của lỗi.</summary>
public static class ImportFields
{
    public const string StudentCode = "studentCode";
    public const string FullName = "fullName";
    public const string Email = "email";
    public const string SpecializationCode = "specializationCode";
    public const string ClassCode = "classCode";
    public const string Phone = "phone";

    /// <summary>Cùng thứ tự với <see cref="StudentImportFile.Headers"/>.</summary>
    public static readonly string[] All = [StudentCode, FullName, Email, ClassCode, Phone];
}
