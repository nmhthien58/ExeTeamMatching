using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace Notification.Worker.Push;

public sealed record FcmPushMessage(
    string Token,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

public enum FcmSendResult
{
    Sent,
    InvalidToken,
    Failed,
}

public interface IFcmSender
{
    Task<FcmSendResult> SendAsync(FcmPushMessage message, CancellationToken ct);
}

public sealed class FirebaseFcmSender : IFcmSender
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly HttpClient _http;
    private readonly ILogger<FirebaseFcmSender> _logger;
    private readonly GoogleCredential? _credential;
    private readonly string? _projectId;

    public FirebaseFcmSender(HttpClient http, IConfiguration configuration, ILogger<FirebaseFcmSender> logger)
    {
        _http = http;
        _logger = logger;
        var path = configuration["FIREBASE_SERVICE_ACCOUNT_PATH"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            logger.LogWarning("Không có Firebase service account tại FIREBASE_SERVICE_ACCOUNT_PATH; bỏ qua push FCM.");
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            _projectId = json.RootElement.GetProperty("project_id").GetString();
            _credential = CredentialFactory.FromFile<ServiceAccountCredential>(path)
                .ToGoogleCredential().CreateScoped(MessagingScope);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được Firebase service account; bỏ qua push FCM.");
        }
    }

    public async Task<FcmSendResult> SendAsync(FcmPushMessage message, CancellationToken ct)
    {
        if (_credential is null || string.IsNullOrWhiteSpace(_projectId)) return FcmSendResult.Failed;

        try
        {
            var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                cancellationToken: ct);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(_projectId)}/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    token = message.Token,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data,
                },
            });
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return FcmSendResult.Sent;

            var error = await response.Content.ReadAsStringAsync(ct);
            if (error.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase))
                return FcmSendResult.InvalidToken;

            _logger.LogWarning("FCM trả {Status}: {Error}", (int)response.StatusCode, error);
            return FcmSendResult.Failed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi push FCM thất bại.");
            return FcmSendResult.Failed;
        }
    }
}
