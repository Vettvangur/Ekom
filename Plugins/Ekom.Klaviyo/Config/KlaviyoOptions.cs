using Ekom.Klaviyo.Models.Catalog;

namespace Ekom.Klaviyo;

public sealed class KlaviyoOptions
{
    // Global fallback key (optional if every store has its own key)
    public string? PrivateApiKey { get; init; }
    public string ApiBaseUrl { get; init; } = "https://a.klaviyo.com";
    public required string Revision { get; init; }

    public bool Enabled { get; set; } = true;

    public KlaviyoCatalogOptions Catalog { get; set; } = new();
    public KlaviyoOrdersOptions Orders { get; set; } = new();
    public KlaviyoSubscriptionOptions Subscriptions { get; set; } = new();
    public KlaviyoTrackingOptions Tracking { get; set; } = new();

    public string ProfileExternalIdProperty { get; init; } = "email";

    public IReadOnlyCollection<KlaviyoStoreOptions> Stores { get; init; } = [];
    public required string SiteBaseUrl { get; init; } = "";
    public bool Testing { get; init; } = false;
}

public sealed class KlaviyoCatalogOptions
{
    public bool Enabled { get; set; } = true;
    // Feed-only options
    public bool ShowPrice { get; set; } = true;
    public bool ShowInventory { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int InventoryPolicy { get; set; } = 2;
    public string ImageCrop { get; set; } = "";

    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();

    /// <summary>
    /// Defines how the catalog is synchronized with Klaviyo.
    /// FeedPull = Klaviyo pulls from a feed endpoint.
    /// ApiPush = application pushes updates via Catalog API.
    /// </summary>
    public KlaviyoCatalogSyncMode SyncMode { get; set; } = KlaviyoCatalogSyncMode.FeedPull;

    /// <summary>
    /// How deleted/unpublished products are handled
    /// </summary>
    public KlaviyoDeleteMode DeleteMode { get; set; } = KlaviyoDeleteMode.Soft;

}

public sealed class KlaviyoOrdersOptions
{
    public bool Enabled { get; set; } = true;

    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();

    public bool TrackingPlacedOrders { get; set; } = false;
}

public enum KlaviyoCatalogSyncMode
{
    FeedPull,
    ApiPush
}

public sealed class KlaviyoSubscriptionOptions
{
    public bool Enabled { get; set; } = true;

    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();
}

public sealed class KlaviyoTrackingOptions
{
    public bool Enabled { get; set; } = true;

    public bool Search { get; set; } = true;
    public bool AddedToCart { get; set; } = true;
    public bool ViewedCategory { get; set; } = true;
    public bool ViewedProduct { get; set; } = true;
    public bool ActiveOnSite { get; set; } = true;
    public bool CheckoutStarted { get; set; } = true;

    public KlaviyoDispatcherOptions Dispatching { get; init; } = new();
}

public sealed class KlaviyoDispatcherOptions
{
    public int MaxBatchSize { get; init; } = 100;
    public int FlushIntervalSeconds { get; init; } = 2;
    public int MaxQueueSize { get; init; } = 10_000;
    public int MaxConcurrency { get; init; } = 3;
}

public sealed class KlaviyoStoreOptions
{
    public required string Alias { get; set; }

    // Store override key (optional)
    public string? PrivateApiKey { get; init; }
    public string? CheckoutUrl { get; init; }
}
