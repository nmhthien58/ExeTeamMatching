using System.Net;
using System.Text;
using Collab.Services.Files;
using Collab.Services.Reports;

namespace Collab.Tests;

/// <summary>MB-53 – báo cáo PDF: chỉ chủ báo cáo đọc, ghi được.</summary>
public sealed class ReportsApiTests(CollabApiFactory factory) : IClassFixture<CollabApiFactory>
{
    private readonly Guid _owner = Guid.NewGuid();

    private static MultipartFormDataContent Pdf(string fileName = "bao-cao.pdf", string content = "%PDF-1.7 nội dung") =>
        new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName } };

    private async Task<UploadedReportResponse> UploadAsync()
    {
        var response = await factory.ClientForAccount(_owner).PostAsync($"/api/reports/{_owner}", Pdf());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<UploadedReportResponse>(response);
    }

    [Fact]
    public async Task Owner_uploads_under_own_prefix_and_gets_signed_link()
    {
        var report = await UploadAsync();

        Assert.Matches(@"^\d{8}-\d{6}-[0-9a-f]{8}\.pdf$", report.FileName);
        Assert.Contains($"reports/{_owner}/{report.FileName}", report.Url);
        Assert.True(report.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Contains($"reports/{_owner}/{report.FileName}", factory.Storage.Objects.Keys);
    }

    [Fact]
    public async Task Stored_file_keeps_whole_content_after_signature_check()
    {
        var report = await UploadAsync();

        var stored = factory.Storage.Objects[$"reports/{_owner}/{report.FileName}"];
        Assert.StartsWith("%PDF-1.7", Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public async Task Owner_lists_own_reports_and_gets_new_link()
    {
        var report = await UploadAsync();
        var client = factory.ClientForAccount(_owner);

        var list = await CollabApiFactory.ReadAsync<List<ReportResponse>>(await client.GetAsync($"/api/reports/{_owner}"));
        var link = await CollabApiFactory.ReadAsync<DownloadUrlResponse>(
            await client.GetAsync($"/api/reports/{_owner}/{report.FileName}/download-url"));

        Assert.Contains(list, r => r.FileName == report.FileName);
        Assert.Contains(report.FileName, link.Url);
    }

    [Fact]
    public async Task Another_user_cannot_read_write_or_list()
    {
        var report = await UploadAsync();
        var client = factory.ClientForAccount(Guid.NewGuid());
        var path = $"/api/reports/{_owner}";

        foreach (var response in new[]
                 {
                     await client.GetAsync(path),
                     await client.GetAsync($"{path}/{report.FileName}/download-url"),
                     await client.PostAsync(path, Pdf()),
                 })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("not-report-owner", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Admin_is_not_an_owner_either()
    {
        var response = await factory.ClientFor(TestPerson.Admin()).GetAsync($"/api/reports/{_owner}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync($"/api/reports/{_owner}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("bao-cao.docx", "%PDF-1.7")]
    [InlineData("bao-cao.pdf", "không phải pdf")]
    [InlineData("bao-cao.pdf", "%P")]
    public async Task Non_pdf_is_rejected(string fileName, string content)
    {
        var response = await factory.ClientForAccount(_owner).PostAsync($"/api/reports/{_owner}", Pdf(fileName, content));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("20261105-010203-abcdef12.pdf")]
    [InlineData("khong-dung-mau.pdf")]
    public async Task Missing_report_returns_404(string fileName)
    {
        var response = await factory.ClientForAccount(_owner).GetAsync($"/api/reports/{_owner}/{fileName}/download-url");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
