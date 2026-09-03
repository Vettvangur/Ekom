using Ekom.Algolia.Models.Events;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ekom.Algolia.Services;

internal interface IAlgoliaInsightsClient
{
    Task SendEventsAsync(IReadOnlyCollection<AlgoliaInsightsEvent> events, CancellationToken ct = default);
}

internal sealed class AlgoliaInsightsClient : IAlgoliaInsightsClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<AlgoliaInsightsClient> _logger;

    public AlgoliaInsightsClient(HttpClient http, ILogger<AlgoliaInsightsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task SendEventsAsync(IReadOnlyCollection<AlgoliaInsightsEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0)
            return;

        var arr = new JsonArray();

        foreach (var evt in events)
            arr.Add(ToJson(evt));

        var payload = new JsonObject
        {
            ["events"] = arr
        };

        var body = payload.ToJsonString(SerializerOptions);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync("events", content, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogWarning("Algolia Insights failed: {Status} {Body}", response.StatusCode, responseBody);
    }

    private static JsonObject ToJson(AlgoliaInsightsEvent evt)
    {
        var timestamp = evt.Timestamp ?? DateTimeOffset.UtcNow;
        var json = new JsonObject
        {
            ["eventType"] = evt.EventType,
            ["eventName"] = evt.EventName,
            ["index"] = evt.Index,
            ["userToken"] = evt.UserToken,
            ["timestamp"] = timestamp.ToUnixTimeMilliseconds(),
            ["objectIDs"] = new JsonArray(evt.ObjectIds.Select(o => JsonValue.Create(o)).ToArray())
        };

        if (!string.IsNullOrWhiteSpace(evt.QueryId))
            json["queryID"] = evt.QueryId;

        if (!string.IsNullOrWhiteSpace(evt.Currency))
            json["currency"] = evt.Currency;

        if (evt.ObjectData != null && evt.ObjectData.Count > 0)
        {
            var data = new JsonArray();
            foreach (var objectData in evt.ObjectData)
            {
                var item = new JsonObject();
                foreach (var kvp in objectData)
                    item[kvp.Key] = JsonValue.Create(kvp.Value);

                data.Add(item);
            }
            json["objectData"] = data;
        }

        return json;
    }
}
