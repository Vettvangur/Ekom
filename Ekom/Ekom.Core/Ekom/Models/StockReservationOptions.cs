namespace Ekom.Models;

/// <summary>Settings under Ekom:Reservations. Timeout is expressed in minutes.</summary>
public sealed class StockReservationOptions
{
    public bool Enabled { get; set; }
    public double Timeout { get; set; } = 30;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int BatchSize { get; set; } = 100;
    public bool WorkerEnabled { get; set; } = true;
    public TimeSpan CompletedRetention { get; set; } = TimeSpan.FromDays(7);

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero || PollInterval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(PollInterval));
        if (BatchSize < 1 || BatchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(BatchSize));
        if (CompletedRetention < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CompletedRetention));
    }
}
