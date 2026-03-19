namespace Ekom.Algolia.Models.Events;

public sealed class AlgoliaInsightsEvent
{
    public required string EventType { get; init; }
    public required string EventName { get; init; }
    public required string UserToken { get; init; }
    public required string Index { get; init; }
    public required IReadOnlyList<string> ObjectIds { get; init; }

    public string? QueryId { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public IReadOnlyDictionary<string, object?>? ObjectData { get; init; }
}
