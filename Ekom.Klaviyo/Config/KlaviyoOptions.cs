namespace Ekom.Klaviyo;

public sealed class KlaviyoOptions
{
	public required string PrivateApiKey { get; init; }
	public required string ApiBaseUrl { get; init; } = "https://a.klaviyo.com";
	public required string Revision { get; init; } = "2023-10-15";
    public bool Enabled { get; set; } = true;
    public IReadOnlyCollection<string> Stores { get; init; } = [];
    public required string Host { get; init; } = "";

    // batching defaults
    public int MaxBatchSize { get; init; } = 100;          // choose conservatively
	public int FlushIntervalSeconds { get; init; } = 2;    // low latency
	public int MaxQueueSize { get; init; } = 10_000;       // backpressure
}
