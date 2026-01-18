namespace Ekom.Klaviyo;

public sealed class KlaviyoOptions
{
	public required string PrivateApiKey { get; init; }
	public string ApiBaseUrl { get; init; } = "https://a.klaviyo.com";
	public string Revision { get; init; } = "2023-10-15";
    public bool Enabled { get; set; } = true;
    public IReadOnlyCollection<string> Stores { get; init; } = [];

    // batching defaults
    public int MaxBatchSize { get; init; } = 100;          // choose conservatively
	public int FlushIntervalSeconds { get; init; } = 2;    // low latency
	public int MaxQueueSize { get; init; } = 10_000;       // backpressure
}
