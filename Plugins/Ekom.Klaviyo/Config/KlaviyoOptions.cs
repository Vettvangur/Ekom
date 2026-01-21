using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo;

public sealed class KlaviyoOptions
{
    public required string PrivateApiKey { get; init; }
    public string ApiBaseUrl { get; init; } = "https://a.klaviyo.com";
    public required string Revision { get; init; }

    public bool Enabled { get; set; } = true;

    public KlaviyoCatalogOptions Catalog { get; set; } = new();
    public KlaviyoEventsOptions Events { get; set; } = new();

    public IReadOnlyCollection<string> Stores { get; init; } = [];
    public required string Host { get; init; } = "";
}

public sealed class KlaviyoCatalogOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Defines how the catalog is synchronized with Klaviyo.
    /// Feed = Klaviyo pulls from a feed endpoint.
    /// SyncEvents = application pushes updates via Catalog API.
    /// </summary>
    public KlaviyoCatalogMethods Method { get; set; } = KlaviyoCatalogMethods.Feed;

    /// <summary>
    /// How deleted/unpublished products are handled when using SyncEvents.
    /// </summary>
    public KlaviyoDeleteMode DeleteMode { get; set; } = KlaviyoDeleteMode.Soft;

    // Feed-only options
    public bool HidePrice { get; set; } = false;
    public bool ShowInventory { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int InventoryPolicy { get; set; } = 2;
    public string ImageCrop { get; set; } = "";

    /// <summary>
    /// Dispatcher settings used when Method == SyncEvents.
    /// </summary>
    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();
}

public sealed class KlaviyoEventsOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Dispatcher settings for event tracking (Placed Order, etc.).
    /// </summary>
    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();
}


public enum KlaviyoCatalogMethods
{
    SyncEvents,
    Feed
}

public sealed class KlaviyoDispatcherOptions
{
    public int MaxBatchSize { get; init; } = 100;
    public int FlushIntervalSeconds { get; init; } = 2;
    public int MaxQueueSize { get; init; } = 10_000;
    public int MaxConcurrency { get; init; } = 3;
}
