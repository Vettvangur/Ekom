namespace Ekom.Tracking;

public sealed class MetaPurchaseRequest
{
    public string StoreAlias { get; set; } = string.Empty;
    public string EventName { get; set; } = "Purchase";
    public long? EventTimeUnix { get; set; }
    public string? EventId { get; set; }
    public string? EventSourceUrl { get; set; }
    public string ActionSource { get; set; } = "website";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Fbp { get; set; }
    public string? Fbc { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Medium { get; set; }
    public string? Campaign { get; set; }
    public string? Term { get; set; }
    public string? Content { get; set; }
    public string? Gclid { get; set; }
    public Dictionary<string, object?> UserData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> CustomData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MetaPurchaseContent> Contents { get; set; } = [];
}

public sealed class MetaPurchaseContent
{
    public string? Id { get; set; }
    public decimal Quantity { get; set; }
    public decimal ItemPrice { get; set; }
}
