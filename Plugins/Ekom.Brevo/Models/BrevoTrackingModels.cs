namespace Ekom.Brevo.Models;

public enum BrevoQueueState { Pending, Sending, Accepted, Failed, Unknown }

/// <summary>A recipient event is separate from API acceptance. Events are returned in occurrence order.</summary>
public sealed record BrevoEmailEvent(string Recipient, string Event, DateTime OccurredUtc, string? Reason);

public sealed record BrevoEmailStatus(string CorrelationId, string StoreAlias, string? OrderId,
    BrevoQueueState State, int Attempts, DateTime CreatedUtc, DateTime UpdatedUtc,
    string? MessageId, int? HttpStatus, IReadOnlyList<BrevoEmailEvent> Events);
