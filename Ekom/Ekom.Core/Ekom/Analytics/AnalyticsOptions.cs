namespace Ekom.Analytics;

public sealed class AnalyticsOptions
{
    public bool Enabled { get; set; }
    public bool ScheduledRefreshEnabled { get; set; } = true;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan LookbackWindow { get; set; } = TimeSpan.FromDays(7);
    public int BatchSize { get; set; } = 250;
    public TimeSpan DelayBetweenBatches { get; set; } = TimeSpan.FromMilliseconds(250);
    public int DatabaseCommandTimeoutSeconds { get; set; } = 30;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public int CustomerIdentityPolicyVersion { get; set; } = 1;

    public bool IsValid(out string? error)
    {
        var maximumTimerDuration = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);
        var maximumLookback = TimeSpan.FromDays(366);
        error = RefreshInterval <= TimeSpan.Zero ? "RefreshInterval must be positive."
            : RefreshInterval > maximumTimerDuration ? "RefreshInterval cannot exceed the maximum timer duration."
            : LookbackWindow < TimeSpan.Zero ? "LookbackWindow cannot be negative."
            : LookbackWindow > maximumLookback ? "LookbackWindow cannot exceed 366 days."
            : BatchSize <= 0 ? "BatchSize must be positive."
            : BatchSize > 10000 ? "BatchSize cannot exceed 10000 orders."
            : DelayBetweenBatches < TimeSpan.Zero ? "DelayBetweenBatches cannot be negative."
            : DelayBetweenBatches > maximumTimerDuration ? "DelayBetweenBatches cannot exceed the maximum timer duration."
            : DatabaseCommandTimeoutSeconds <= 0 ? "DatabaseCommandTimeoutSeconds must be positive."
            : DatabaseCommandTimeoutSeconds > int.MaxValue / 1000 ? "DatabaseCommandTimeoutSeconds is too large for a millisecond command timeout."
            : LeaseDuration <= TimeSpan.Zero ? "LeaseDuration must be positive."
            : LeaseDuration > maximumTimerDuration ? "LeaseDuration cannot exceed the maximum timer duration."
            : LeaseDuration < TimeSpan.FromSeconds((long)DatabaseCommandTimeoutSeconds + 5) ? "LeaseDuration must exceed the command timeout by at least five seconds."
            : DelayBetweenBatches >= LeaseDuration ? "DelayBetweenBatches must be shorter than LeaseDuration."
            : CustomerIdentityPolicyVersion <= 0 ? "CustomerIdentityPolicyVersion must be positive."
            : null;
        return error == null;
    }
}
