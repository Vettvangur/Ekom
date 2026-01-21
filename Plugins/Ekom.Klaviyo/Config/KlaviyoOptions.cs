using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo;

public sealed class KlaviyoOptions
{
	public required string PrivateApiKey { get; init; }
	public required string ApiBaseUrl { get; init; } = "https://a.klaviyo.com";
	public required string Revision { get; init; }
    public bool Enabled { get; set; } = true; // Enable/Disable all features

    public KlaviyoProductFeedOptions ProductFeed { get; set; } = new()
    {
        Enabled = true
    };
    public KlaviyoProductEventsOptions ProductEvents { get; set; } = new()
    {
        Enabled = true
    };

    public IReadOnlyCollection<string> Stores { get; init; } = [];
    public required string Host { get; init; } = "";

    // batching defaults
    public int MaxBatchSize { get; init; } = 100;          // choose conservatively
	public int FlushIntervalSeconds { get; init; } = 2;    // low latency
	public int MaxQueueSize { get; init; } = 10_000;       // backpressure
}
public sealed class KlaviyoProductFeedOptions
{
    public bool Enabled { get; set; } = true;
    public bool HidePrice { get; set; } = false;
    public bool ShowInventory { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int InventoryPolicy { get; set; } = 2; // 1 or 2
    public string ImageCrop { get; set; } = "";
}

public sealed class KlaviyoProductEventsOptions
{
    public bool Enabled { get; set; } = true;
    public KlaviyoDeleteMode DeleteMode { get; set; } = KlaviyoDeleteMode.Soft;
}
