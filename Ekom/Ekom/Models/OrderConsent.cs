namespace Ekom.Models;

public sealed class OrderConsent
{
    public bool? Analytics { get; set; }
    public bool? Marketing { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? Source { get; set; }

    public OrderConsent Clone()
        => new()
        {
            Analytics = Analytics,
            Marketing = Marketing,
            ResolvedAtUtc = ResolvedAtUtc,
            Source = Source
        };
}
