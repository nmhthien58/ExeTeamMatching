using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academic.Repositories.Catalogs;
using Academic.Repositories.Students;
using Academic.Services;
using Academic.Services.Imports;
using Academic.Services.Semesters;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Academic.Tests;

/// <summary>
/// API import danh sách sinh viên (BE-17) với academic_db và Redis thật. Bật bằng <c>EXE_INTEGRATION=1</c>;
/// container <c>academic-api</c> phải tắt (xem <see cref="SemestersRoundsApiTests"/>).
/// Dọn học kỳ, mã lớp, sinh viên, danh sách học kỳ, lô import và outbox thử khi xong.
/// </summary>
[Collection(RoundTestsCollection.Name)]
public sealed class ImportsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly AcademicApiFactory _api = new();
    private readonly List<Guid> _semesters = [];
    private readonly List<int> _classCodes = [];
    private readonly List<string> _studentCodes = [];
    private HttpClient? _admin;
    private HttpClient Admin => _admin ??= _api.ClientAs(Roles.Admin);

    [Fact]
    public async Task Only_admin_can_use_imports_and_template_is_readable()
    {
        if (!Enabled)
        {
            return;
        }

        var template = await Admin.GetAsync("/api/imports/students/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", template.Content.Headers.ContentType!.MediaType);
        Assert.Single(StudentImportFile.Read(await template.Content.ReadAsStreamAsync()));

        var student = _api.ClientAs(Roles.Student);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/imports/students/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/imports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync($"/api/imports/students/{Guid.NewGuid()}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/imports")).StatusCode);
    }

    [Fact]
    public async Task Preview_then_confirm_imports_valid_rows_and_publishes_student_imported()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var classCode = await CreateClassCodeAsync();
        var specialization = await _api.QueryDbAsync(db => db.Specializations.FirstAsync(s => s.IsActive));
        var existing = await CreateStudentAsync(specialization.Id);
        string a = NewCode(), b = NewCode();

        var preview = await PreviewAsync(semester.Id, "EXE101",
            [a, "Sinh Viên A", $"{a}@example.test", specialization.Code, classCode.Code.ToLowerInvariant(), ""],
            [b, "Sinh Viên B", $"{b}@example.test", specialization.Code, classCode.Code, "090 123 4567"],
            [existing.StudentCode, "Tên Mới", existing.Email, specialization.Code, classCode.Code, ""],
            ["SE12", "Sai MSSV", "sai@example.test", specialization.Code, classCode.Code, ""],
            [a, "Trùng MSSV", "trung@example.test", specialization.Code, classCode.Code, ""],
            [NewCode(), "Thiếu chuyên ngành", "thieu@example.test", "", classCode.Code, ""]);

        Assert.Equal((6, 3, 3), (preview.TotalRows, preview.ValidRows.Count, preview.ErrorRows.Count));
        Assert.Equal("0901234567", preview.ValidRows.Single(r => r.StudentCode == b).Phone);
        Assert.Equal([5, 6, 7], preview.ErrorRows.Select(r => r.RowNumber));
        Assert.Equal("specializationCode", preview.ErrorRows[2].Errors.Single().Field);
        Assert.False(await _api.QueryDbAsync(db => db.Students.AnyAsync(s => s.StudentCode == a))); // xem trước chưa ghi

        var confirm = await Admin.PostAsync($"/api/imports/students/{preview.PreviewId}/confirm", null);
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());
        var batch = (await confirm.Content.ReadFromJsonAsync<ImportBatchResponse>())!;
        Assert.Equal((semester.Id, "danh-sach.xlsx", 6, 3, 3, "COMPLETED", "Người thử"),
            (batch.SemesterId, batch.FileName, batch.TotalRows, batch.SuccessRows, batch.FailedRows, batch.Status, batch.ImportedByName));

        var codes = new[] { a, b, existing.StudentCode };
        var students = await _api.QueryDbAsync(db => db.Students.Where(s => codes.Contains(s.StudentCode)).ToListAsync());
        Assert.Equal(3, students.Count);
        Assert.Equal("Tên Mới", students.Single(s => s.Id == existing.Id).FullName);
        Assert.Equal("0901234567", students.Single(s => s.StudentCode == b).Phone);
        var ids = students.Select(s => s.Id).ToList();
        var enrollments = await _api.QueryDbAsync(db => db.SemesterEnrollments.Where(e => e.SemesterId == semester.Id).ToListAsync());
        Assert.Equal(ids.Order(), enrollments.Select(e => e.StudentId).Order());
        Assert.All(enrollments, e => Assert.Equal(("EXE101", classCode.Id, (Guid?)batch.Id), (e.CourseCode, e.ClassCodeId, e.ImportBatchId)));
        var savedBatch = await _api.QueryDbAsync(db => db.ImportBatches.SingleAsync(x => x.Id == batch.Id));
        Assert.Contains("Trùng MSSV với dòng 2.", savedBatch.Errors);

        var events = await _api.QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => ids.Contains(m.AggregateId)).ToListAsync());
        Assert.Equal(3, events.Count);
        Assert.All(events, m => Assert.Equal(("exe.student", "StudentImported"), (m.Topic, m.EventType)));
        var payload = JsonDocument.Parse(events.Single(m => m.AggregateId == students.Single(s => s.StudentCode == b).Id).Payload).RootElement;
        Assert.Equal((b, "0901234567", semester.Id.ToString(), semester.EndDate.ToString("yyyy-MM-dd"), "EXE101", classCode.Code, specialization.Id),
            (payload.GetProperty("studentCode").GetString(), payload.GetProperty("phone").GetString(), payload.GetProperty("semesterId").GetString(),
             payload.GetProperty("semesterEndDate").GetString(), payload.GetProperty("courseCode").GetString(), payload.GetProperty("classCode").GetString(),
             payload.GetProperty("specializationId").GetInt32()));

        // Bản xem trước chỉ xác nhận một lần.
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.PostAsync($"/api/imports/students/{preview.PreviewId}/confirm", null)).StatusCode);

        var history = await Admin.GetFromJsonAsync<PagedResponse<ImportBatchResponse>>($"/api/imports?semesterId={semester.Id}");
        Assert.Equal((1, batch.Id, "Người thử"), (history!.TotalCount, history.Items.Single().Id, history.Items.Single().ImportedByName));
    }

    [Fact]
    public async Task Reimport_updates_class_of_same_course_and_rejects_other_course()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var first = await CreateClassCodeAsync();
        var second = await CreateClassCodeAsync();
        var specialization = await _api.QueryDbAsync(db => db.Specializations.FirstAsync(s => s.IsActive));
        var code = NewCode();
        string[] Row(ClassCode c) => [code, "Sinh Viên C", $"{code}@example.test", specialization.Code, c.Code, ""];

        await ConfirmAsync(await PreviewAsync(semester.Id, "EXE101", Row(first)));

        var otherCourse = await PreviewAsync(semester.Id, "EXE201", Row(second));
        Assert.Contains("ở môn EXE101", otherCourse.ErrorRows.Single().Errors.Single().Message);
        var failed = await ConfirmAsync(otherCourse);
        Assert.Equal(("FAILED", 0, 1), (failed.Status, failed.SuccessRows, failed.FailedRows));

        await ConfirmAsync(await PreviewAsync(semester.Id, "EXE101", Row(second)));
        var enrollment = await _api.QueryDbAsync(db => db.SemesterEnrollments.SingleAsync(e => e.SemesterId == semester.Id));
        Assert.Equal(("EXE101", second.Id), (enrollment.CourseCode, enrollment.ClassCodeId));

        var history = await Admin.GetFromJsonAsync<PagedResponse<ImportBatchResponse>>($"/api/imports?semesterId={semester.Id}&pageSize=2");
        Assert.Equal((3, 2, 2), (history!.TotalCount, history.TotalPages, history.Items.Count));
        Assert.Equal("COMPLETED", history.Items[0].Status); // mới nhất trước
    }

    [Fact]
    public async Task Preview_rejects_bad_form_and_bad_file()
    {
        if (!Enabled)
        {
            return;
        }

        var semester = await CreateSemesterAsync();
        var noFile = new MultipartFormDataContent { { new StringContent(semester.Id.ToString()), "semesterId" }, { new StringContent("EXE101"), "courseCode" } };
        await AssertBadRequestAsync(noFile, "file");

        await AssertBadRequestAsync(Form(Guid.NewGuid(), "EXE101", Excel(StudentImportFile.Headers.ToArray(), ["SE123456", "A", "a@x.vn", "SE", "EXE101SE01", ""])), "semesterId");
        await AssertBadRequestAsync(Form(semester.Id, "EXE301", Excel(StudentImportFile.Headers.ToArray())), "courseCode");
        await AssertBadRequestAsync(Form(semester.Id, "EXE101", "không phải excel"u8.ToArray(), "danh-sach.csv"), "file");
        await AssertBadRequestAsync(Form(semester.Id, "EXE101", Excel(["MSSV", "Họ tên"], ["SE123456", "A"])), "file");
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.GetAsync("/api/imports?pageSize=101")).StatusCode);
    }

    private async Task AssertBadRequestAsync(MultipartFormDataContent form, string field)
    {
        var response = await Admin.PostAsync("/api/imports/students/preview", form);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty(field, out _), body);
    }

    private async Task<ImportPreviewResponse> PreviewAsync(Guid semesterId, string course, params string[][] rows)
    {
        var response = await Admin.PostAsync("/api/imports/students/preview", Form(semesterId, course, Excel(StudentImportFile.Headers.ToArray(), rows)));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ImportPreviewResponse>())!;
    }

    private async Task<ImportBatchResponse> ConfirmAsync(ImportPreviewResponse preview)
    {
        var response = await Admin.PostAsync($"/api/imports/students/{preview.PreviewId}/confirm", null);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ImportBatchResponse>())!;
    }

    private static MultipartFormDataContent Form(Guid semesterId, string course, byte[] file, string fileName = "danh-sach.xlsx")
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent
        {
            { content, "file", fileName },
            { new StringContent(semesterId.ToString()), "semesterId" },
            { new StringContent(course), "courseCode" },
        };
    }

    private static byte[] Excel(string[] header, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("SinhVien");
        foreach (var (row, r) in rows.Prepend(header).Select((row, r) => (row, r)))
        {
            for (var c = 0; c < row.Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = row[c];
            }
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private string NewCode()
    {
        var code = $"ZX{Random.Shared.Next(0, 1_000_000):D6}";
        _studentCodes.Add(code);
        return code;
    }

    private async Task<Student> CreateStudentAsync(int specializationId)
    {
        var code = NewCode();
        return await _api.QueryDbAsync(async db =>
        {
            var s = new Student { Id = Guid.NewGuid(), StudentCode = code, FullName = "Tên Cũ", Email = $"{code.ToLowerInvariant()}@example.test", SpecializationId = specializationId };
            db.Students.Add(s);
            await db.SaveChangesAsync();
            return s;
        });
    }

    private async Task<ClassCode> CreateClassCodeAsync()
    {
        var classCode = await _api.QueryDbAsync(async db =>
        {
            var c = new ClassCode { Code = $"ZZIMPORT{Random.Shared.Next(0, 1_000_000):D6}" };
            db.ClassCodes.Add(c);
            await db.SaveChangesAsync();
            return c;
        });
        _classCodes.Add(classCode.Id);
        return classCode;
    }

    private async Task<SemesterResponse> CreateSemesterAsync()
    {
        var used = await _api.QueryDbAsync(db => db.Semesters.Select(s => s.Code).ToListAsync());
        var code = Enumerable.Range(0, 100).Select(n => $"SU{n:00}").First(c => !used.Contains(c));
        var response = await Admin.PostAsJsonAsync("/api/semesters", new { code, name = "Học kỳ thử import", startDate = "2032-05-03", endDate = "2032-08-28" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var semester = (await response.Content.ReadFromJsonAsync<SemesterResponse>())!;
        _semesters.Add(semester.Id);
        return semester;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var students = await db.Students.Where(s => _studentCodes.Contains(s.StudentCode)).Select(s => s.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => students.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.SemesterEnrollments.Where(e => students.Contains(e.StudentId) || _semesters.Contains(e.SemesterId)).ExecuteDeleteAsync();
                await db.ImportBatches.Where(b => _semesters.Contains(b.SemesterId)).ExecuteDeleteAsync();
                await db.Students.Where(s => students.Contains(s.Id)).ExecuteDeleteAsync();
                await db.ClassCodes.Where(c => _classCodes.Contains(c.Id)).ExecuteDeleteAsync();
                return await db.Semesters.Where(s => _semesters.Contains(s.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
