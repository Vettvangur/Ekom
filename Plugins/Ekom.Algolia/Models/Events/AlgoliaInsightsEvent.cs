namespace Ekom.Algolia.Models.Events;

public sealed class AlgoliaInsightsEvent
{
    public required string EventType { get; init; }
    public required string EventName { get; init; }
    public required string UserToken { get; init; }
    public required string Index { get; init; }
    public required IReadOnlyList<string> ObjectIds { get; init; }

    public string? QueryId { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>>? ObjectData { get; init; }
}
