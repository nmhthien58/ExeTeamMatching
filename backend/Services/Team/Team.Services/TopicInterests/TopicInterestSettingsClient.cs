using System.Net.Http.Json;
using System.Text.Json;

namespace Team.Services.TopicInterests;

public sealed record TopicInterestSettings(int InterestExtraSlots, int MaxInterestsUnlimited);

public interface ITopicInterestSettingsClient
{
    Task<TopicInterestSettings> GetAsync(CancellationToken ct);
}

public sealed class TopicInterestSettingsClient(HttpClient http) : ITopicInterestSettingsClient
{
    public async Task<TopicInterestSettings> GetAsync(CancellationToken ct)
    {
        try
        {
            var data = await http.GetFromJsonAsync<RuleSettingsResponse>("/api/rule-settings", ct);
            if (data?.InterestExtraSlots is not >= 0 or > 10 || data.MaxInterestsUnlimited is not >= 1 or > 100)
                throw new HttpRequestException("Cấu hình quy tắc không hợp lệ.");
            return new TopicInterestSettings(data.InterestExtraSlots.Value, data.MaxInterestsUnlimited.Value);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Không đọc được cấu hình quy tắc từ Academic.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Không đọc được cấu hình quy tắc từ Academic.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("Không đọc được cấu hình quy tắc từ Academic.");
        }
    }

    private sealed record RuleSettingsResponse(int? InterestExtraSlots, int? MaxInterestsUnlimited);
}
