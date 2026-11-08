using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Errors;
using ClosedXML.Excel;

namespace Academic.Services.Imports;

public sealed record LecturerImportRow(int RowNumber, string Email, string FullName, string? Phone);
public sealed record LecturerImportRowError(string Field, string Message);
public sealed record LecturerImportErrorRow(
    int RowNumber, string Email, string FullName, string? Phone, IReadOnlyList<LecturerImportRowError> Errors);

public sealed record LecturerImportPreviewResponse(
    Guid PreviewId, string FileName, int TotalRows,
    IReadOnlyList<LecturerImportRow> ValidRows,
    IReadOnlyList<LecturerImportErrorRow> ErrorRows);

public sealed record LecturerImportConfirmResult(
    int TotalRows, int SuccessRows, int FailedRows, IReadOnlyList<string> Errors);

public sealed record LecturerImportPreviewInput(Stream? File, string? FileName, long FileLength);

public static class LecturerImportFile
{
    public const int MaxRows = 1000;
    public static readonly IReadOnlyList<string> Headers = ["Email", "Họ và tên", "SĐT"];

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["email"] = "email", ["mail"] = "email", ["thudientu"] = "email",
        ["hoten"] = "fullName", ["hovaten"] = "fullName", ["fullname"] = "fullName", ["tengiangvien"] = "fullName", ["giangvien"] = "fullName",
        ["sdt"] = "phone", ["sodienthoai"] = "phone", ["phone"] = "phone", ["dienthoai"] = "phone"
    };

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("GiangVien");
        for (var i = 0; i < Headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(2).Style.NumberFormat.Format = "@";
        sheet.Column(3).Style.NumberFormat.Format = "@";

        string[] example = ["gv.nguyenvana@fpt.edu.vn", "Nguyễn Văn A", "0901234567"];
        for (var i = 0; i < example.Length; i++)
        {
            sheet.Cell(2, i + 1).Value = example[i];
        }
        sheet.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public static IReadOnlyList<LecturerImportRow> Read(Stream stream)
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
            if (!columns.ContainsKey("email") || !columns.ContainsKey("fullName"))
            {
                throw FileError("Thiếu cột bắt buộc: Email, Họ và tên. Tải file mẫu để xem đúng tiêu đề.");
            }

            var rows = new List<LecturerImportRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                if (row.IsEmpty()) continue;

                var email = CellText(row.Cell(columns["email"]));
                var fullName = CellText(row.Cell(columns["fullName"]));
                var phone = columns.TryGetValue("phone", out var pCol) ? CellText(row.Cell(pCol)) : null;

                if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(phone))
                {
                    continue;
                }

                rows.Add(new LecturerImportRow(rowNumber, email, fullName, string.IsNullOrWhiteSpace(phone) ? null : phone));
                if (rows.Count > MaxRows)
                {
                    throw FileError($"File vượt quá số dòng tối đa ({MaxRows} dòng).");
                }
            }

            if (rows.Count == 0)
            {
                throw FileError("File không có dòng dữ liệu nào.");
            }

            return rows;
        }
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
