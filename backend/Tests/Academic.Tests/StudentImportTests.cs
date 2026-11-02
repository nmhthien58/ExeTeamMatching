using Academic.Services.Imports;
using BuildingBlocks.Errors;
using ClosedXML.Excel;

namespace Academic.Tests;

/// <summary>Đọc file Excel và quy tắc từng dòng của import sinh viên (BE-17), không cần DB.</summary>
public sealed class StudentImportTests
{
    private static readonly Guid Existing = Guid.NewGuid();
    private static readonly Guid EnrolledOther = Guid.NewGuid();

    private static readonly ImportReference Reference = new(
        [new CatalogEntry(1, "SE", "Kỹ thuật phần mềm", true), new CatalogEntry(2, "GD", "Thiết kế đồ họa", true), new CatalogEntry(3, "OLD", "Ngừng", false)],
        [new CatalogEntry(10, "EXE101SE01", null, true), new CatalogEntry(11, "EXE101OLD", null, false)],
        [new ExistingStudent(Existing, "SE111111", "old@example.com"), new ExistingStudent(EnrolledOther, "SE222222", "two@example.com")],
        new Dictionary<Guid, string> { [EnrolledOther] = "EXE201" },
        ["mentor@example.com"]);

    [Fact]
    public void Template_can_be_read_back()
    {
        var rows = StudentImportFile.Read(new MemoryStream(StudentImportFile.CreateTemplate()));

        var row = Assert.Single(rows);
        Assert.Equal(new ImportRow(2, "SE184118", "Nguyễn Văn A", "a.nguyen@example.com", "SE", "EXE101SE01", "0901234567"), row);
    }

    [Fact]
    public void Reads_columns_by_header_name_in_any_order_and_skips_blank_rows()
    {
        var file = Workbook(["sdt", "Mã lớp", "HỌ VÀ TÊN", "email", "Mã sinh viên", "Chuyên ngành", "Ghi chú"],
            [901234567d, "EXE101SE01", " Trần B ", "b@example.com", "se123456", "SE", "bỏ qua"],
            [],
            ["", "EXE101SE01", "Lê C", "c@example.com", "SE654321", "SE", ""]);

        var rows = StudentImportFile.Read(file);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new ImportRow(2, "se123456", "Trần B", "b@example.com", "SE", "EXE101SE01", "901234567"), rows[0]);
        Assert.Equal((4, (string?)null), (rows[1].RowNumber, rows[1].Phone));
    }

    [Fact]
    public void File_without_required_column_or_rows_or_not_xlsx_is_rejected()
    {
        var missing = Assert.Throws<RequestValidationException>(() => StudentImportFile.Read(Workbook(["MSSV", "Họ tên", "Email"], ["SE123456", "A", "a@x.vn"])));
        Assert.Contains("Chuyên ngành", missing.Errors["file"][0]);
        Assert.Contains("Mã lớp", missing.Errors["file"][0]);

        Assert.Throws<RequestValidationException>(() => StudentImportFile.Read(Workbook(StudentImportFile.Headers.Cast<object>().ToArray())));
        Assert.Throws<RequestValidationException>(() => StudentImportFile.Read(new MemoryStream("không phải excel"u8.ToArray())));
    }

    [Fact]
    public void Valid_row_is_normalized()
    {
        var check = Single(Row(2, "se184118", "Nguyễn Văn A", "A@Example.com", "se", "exe101se01", "090 123.4567"));

        Assert.True(check.IsValid);
        Assert.Equal(new ImportRow(2, "SE184118", "Nguyễn Văn A", "A@Example.com", "SE", "EXE101SE01", "0901234567"), check.Row);
        Assert.Equal((1, 10, (Guid?)null), (check.SpecializationId, check.ClassCodeId, check.ExistingStudentId));
    }

    [Fact]
    public void Specialization_can_be_given_by_full_name()
    {
        var check = Single(Row(2, specialization: "thiết kế đồ họa"));
        Assert.Equal((2, "GD"), (check.SpecializationId, check.Row.SpecializationCode));
    }

    [Theory]
    [InlineData("", "studentCode", "Thiếu MSSV.")]
    [InlineData("SE12345", "studentCode", "MSSV phải")]
    [InlineData("S1234567", "studentCode", "MSSV phải")]
    [InlineData("SE1234567", "studentCode", "MSSV phải")]
    public void Student_code_must_be_two_letters_and_six_digits(string code, string field, string message) =>
        AssertError(Row(2, code: code), field, message);

    [Theory]
    [InlineData("", "Thiếu email.")]
    [InlineData("khong-co-a-cong", "Email không đúng định dạng.")]
    [InlineData("a b@example.com", "Email không đúng định dạng.")]
    [InlineData("a@localhost", "Email không đúng định dạng.")]
    public void Email_must_be_valid(string email, string message) => AssertError(Row(2, email: email), "email", message);

    [Theory]
    [InlineData("", "Thiếu chuyên ngành.")]
    [InlineData("XX", "không có trong danh mục")]
    [InlineData("OLD", "đã ngừng dùng")]
    public void Specialization_is_required_and_must_be_active(string specialization, string message) =>
        AssertError(Row(2, specialization: specialization), "specializationCode", message);

    [Theory]
    [InlineData("", "Thiếu mã lớp.")]
    [InlineData("EXE101ZZ99", "không có trong danh mục")]
    [InlineData("EXE101OLD", "đã ngừng dùng")]
    public void Class_code_is_required_and_must_be_active(string classCode, string message) =>
        AssertError(Row(2, classCode: classCode), "classCode", message);

    [Theory]
    [InlineData("12345678")]
    [InlineData("090123456789012")] // 15 chữ số
    [InlineData("0901-abc-567")]
    public void Phone_is_optional_but_must_be_digits(string phone) => AssertError(Row(2, phone: phone), "phone", "SĐT");

    [Fact]
    public void Missing_name_and_several_errors_are_reported_together()
    {
        var check = Single(Row(2, code: "bad", name: "", email: "x", specialization: ""));
        Assert.Equal(["studentCode", "fullName", "email", "specializationCode"], check.Errors.Select(e => e.Field));
    }

    [Fact]
    public void Duplicate_code_or_email_in_file_marks_later_rows()
    {
        var checks = StudentImportRules.Check(
            [Row(2, code: "SE300001", email: "a@example.com"), Row(3, code: "se300001", email: "b@example.com"), Row(4, code: "SE300002", email: "A@EXAMPLE.COM")],
            "EXE101", Reference);

        Assert.True(checks[0].IsValid);
        Assert.Equal("Trùng MSSV với dòng 2.", Assert.Single(checks[1].Errors).Message);
        Assert.Equal("Trùng email với dòng 2.", Assert.Single(checks[2].Errors).Message);
    }

    [Fact]
    public void Existing_student_is_updated_but_email_of_another_student_is_rejected()
    {
        var update = Single(Row(2, code: "SE111111", email: "new@example.com"));
        Assert.True(update.IsValid);
        Assert.Equal(Existing, update.ExistingStudentId);

        AssertError(Row(2, code: "SE999999", email: "OLD@example.com"), "email", "Email đã thuộc sinh viên SE111111.");
    }

    [Fact]
    public void Student_already_in_semester_for_another_course_is_rejected()
    {
        AssertError(Row(2, code: "SE222222", email: "two@example.com"), "studentCode", "ở môn EXE201");
        Assert.True(StudentImportRules.Check([Row(2, code: "SE222222", email: "two@example.com")], "EXE201", Reference).Single().IsValid);
    }

    private static ImportRow Row(int number, string code = "SE184118", string name = "Nguyễn Văn A", string email = "a@example.com",
        string specialization = "SE", string classCode = "EXE101SE01", string? phone = null) =>
        new(number, code, name, email, specialization, classCode, phone);

    [Fact]
    public void Email_of_a_mentor_is_a_row_error_in_any_letter_case()
    {
        var error = Single(Row(2, code: "SE333333", email: "MENTOR@example.com")).Errors.Single();
        Assert.Equal((ImportFields.Email, "Email đã thuộc một mentor."), (error.Field, error.Message));
        Assert.True(Single(Row(2, code: "SE333333", email: "khac@example.com")).IsValid);
    }

    private static ImportRowCheck Single(ImportRow row) => StudentImportRules.Check([row], "EXE101", Reference).Single();

    private static void AssertError(ImportRow row, string field, string message)
    {
        var check = Single(row);
        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Field == field && e.Message.Contains(message));
    }

    private static MemoryStream Workbook(params object[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
            }
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
