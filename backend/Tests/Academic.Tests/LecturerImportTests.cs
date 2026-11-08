using System.Globalization;
using Academic.Repositories.Lecturers;
using Academic.Services.Imports;
using Academic.Services.Lecturers;
using BuildingBlocks.Errors;
using ClosedXML.Excel;

namespace Academic.Tests;

/// <summary>Kiểm tra đọc file Excel giảng viên và quy tắc hồ sơ giảng viên (BE-93).</summary>
public sealed class LecturerImportTests
{
    private static MemoryStream Workbook(params object[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("GiangVien");
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
            }
        }
        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Template_can_be_read_back()
    {
        var templateBytes = LecturerImportFile.CreateTemplate();
        using var stream = new MemoryStream(templateBytes);
        var rows = LecturerImportFile.Read(stream);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("gv.nguyenvana@fpt.edu.vn", row.Email);
        Assert.Equal("Nguyễn Văn A", row.FullName);
        Assert.Equal("0901234567", row.Phone);
    }

    [Fact]
    public void Reads_columns_by_header_name_in_any_order_and_skips_blank_rows()
    {
        var file = Workbook(
            ["sdt", "HỌ VÀ TÊN", "Ghi chú", "email"],
            ["0901234567", " Trần Văn B ", "bỏ qua", "gv.tranb@fpt.edu.vn"],
            [],
            ["", "Lê Văn C", "", "gv.lec@fe.edu.vn"]);

        var rows = LecturerImportFile.Read(file);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new LecturerImportRow(2, "gv.tranb@fpt.edu.vn", "Trần Văn B", "0901234567"), rows[0]);
        Assert.Equal(new LecturerImportRow(4, "gv.lec@fe.edu.vn", "Lê Văn C", null), rows[1]);
    }

    [Fact]
    public void File_without_required_column_or_empty_is_rejected()
    {
        var missingEmail = Assert.Throws<RequestValidationException>(() =>
            LecturerImportFile.Read(Workbook(["Họ và tên", "SĐT"], ["Nguyễn Văn A", "0901234567"])));
        Assert.Contains("Thiếu cột bắt buộc", missingEmail.Errors["file"][0]);

        var missingName = Assert.Throws<RequestValidationException>(() =>
            LecturerImportFile.Read(Workbook(["Email", "SĐT"], ["a@fpt.edu.vn", "0901234567"])));
        Assert.Contains("Thiếu cột bắt buộc", missingName.Errors["file"][0]);

        var empty = Assert.Throws<RequestValidationException>(() =>
            LecturerImportFile.Read(Workbook(["Email", "Họ và tên"])));
        Assert.Contains("không có dòng dữ liệu nào", empty.Errors["file"][0]);
    }

    [Fact]
    public void Profile_input_validator_checks_desired_teams_and_domains()
    {
        var validator = new LecturerProfileInputValidator();

        var valid = new LecturerProfileInput([1, 2], 3, 2);
        Assert.True(validator.Validate(valid).IsValid);

        var invalidCap = new LecturerProfileInput([1], -1, 201);
        var result = validator.Validate(invalidCap);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DesiredTeamsExe101");
        Assert.Contains(result.Errors, e => e.PropertyName == "DesiredTeamsExe201");
    }
}
