using System.Text;
using Collab.Services.Storage;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Collab.Tests;

/// <summary>
/// Chạy với hạ tầng thật (BE-63). Bật bằng <c>EXE_INTEGRATION=1</c>:
/// <list type="bullet">
/// <item>Kho file S3: <c>docker compose up -d object-storage</c>.</item>
/// <item>Hub: Gateway + Collab đang chạy; <c>EXE_HUB_URL</c>, hai token sinh viên cùng nhóm
/// (<c>EXE_HUB_TOKEN_A</c>, <c>EXE_HUB_TOKEN_B</c>), token người ngoài (<c>EXE_HUB_TOKEN_OUTSIDER</c>),
/// <c>EXE_HUB_WORKSPACE</c>, <c>EXE_HUB_TEAM_CHANNEL</c>.</item>
/// </list>
/// </summary>
public sealed class RealtimeAndStorageIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    [Fact]
    public async Task File_upload_then_presigned_download_returns_same_bytes()
    {
        if (!Enabled)
        {
            return;
        }

        using var storage = new S3ObjectStorage(Options.Create(new ObjectStorageOptions
        {
            ServiceUrl = "http://localhost:9000",
            AccessKey = "collab-dev",
            SecretKey = "collab-dev-secret-change-me",
        }));
        await storage.EnsureBucketAsync(CancellationToken.None);

        var key = FileKey.For(Guid.NewGuid(), Guid.NewGuid(), 1);
        var content = Encoding.UTF8.GetBytes("Báo cáo tuần 3 – nội dung thử");
        await storage.PutAsync(key, new MemoryStream(content), "text/plain", "Báo cáo tuần 3.txt", CancellationToken.None);

        var url = storage.GetDownloadUrl(key);
        using var http = new HttpClient();
        var response = await http.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty);

        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.False((await http.GetAsync(storage.GetDownloadUrl(key))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Hub_through_gateway_joins_channel_and_relays_typing()
    {
        if (!Enabled || Env("EXE_HUB_URL") is not { } hubUrl)
        {
            return;
        }

        var workspaceId = Guid.Parse(Env("EXE_HUB_WORKSPACE")!);
        var channelId = Guid.Parse(Env("EXE_HUB_TEAM_CHANNEL")!);
        await using var a = Connect(hubUrl, Env("EXE_HUB_TOKEN_A")!);
        await using var b = Connect(hubUrl, Env("EXE_HUB_TOKEN_B")!);
        await using var outsider = Connect(hubUrl, Env("EXE_HUB_TOKEN_OUTSIDER")!);

        var typing = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        b.On<System.Text.Json.JsonElement>("typing", e => typing.TrySetResult(e.GetProperty("channelId").GetString()!));

        await a.StartAsync();
        await b.StartAsync();
        await outsider.StartAsync();

        await a.InvokeAsync("JoinWorkspace", workspaceId);
        await a.InvokeAsync("JoinChannel", channelId);
        await b.InvokeAsync("JoinChannel", channelId);
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => outsider.InvokeAsync("JoinChannel", channelId));

        await a.InvokeAsync("Typing", channelId);
        var received = await typing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(channelId.ToString(), received);
    }

    private static HubConnection Connect(string url, string token) =>
        new HubConnectionBuilder()
            .WithUrl(url, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .Build();
}
