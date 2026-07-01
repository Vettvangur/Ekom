namespace Ekom.Models;

public sealed class OrderTracking
{
    public DateTime? CapturedAtUtc { get; set; }
    public string? Source { get; set; }
    public string? Medium { get; set; }
    public string? Campaign { get; set; }
    public string? Term { get; set; }
    public string? Content { get; set; }
    public string? ClickId { get; set; }
    public string? ClickIdType { get; set; }
    public string? LandingUrl { get; set; }
    public string? Referrer { get; set; }
    public bool? HasCookieSupport { get; set; }
    public string? CaptureMethod { get; set; }
    public Ga4OrderTracking Ga4 { get; set; } = new();
    public MetaOrderTracking Meta { get; set; } = new();

    public bool HasData()
        => !string.IsNullOrWhiteSpace(Source)
        || !string.IsNullOrWhiteSpace(Medium)
        || !string.IsNullOrWhiteSpace(Campaign)
        || !string.IsNullOrWhiteSpace(Term)
        || !string.IsNullOrWhiteSpace(Content)
        || !string.IsNullOrWhiteSpace(ClickId)
        || !string.IsNullOrWhiteSpace(LandingUrl)
        || !string.IsNullOrWhiteSpace(Referrer)
        || Ga4.HasData()
        || Meta.HasData();

    public OrderTracking Clone()
        => new()
        {
            CapturedAtUtc = CapturedAtUtc,
            Source = Source,
            Medium = Medium,
            Campaign = Campaign,
            Term = Term,
            Content = Content,
            ClickId = ClickId,
            ClickIdType = ClickIdType,
            LandingUrl = LandingUrl,
            Referrer = Referrer,
            HasCookieSupport = HasCookieSupport,
            CaptureMethod = CaptureMethod,
            Ga4 = Ga4.Clone(),
            Meta = Meta.Clone()
        };
}

public sealed class Ga4OrderTracking
{
    public string? ClientId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, string?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasData()
        => !string.IsNullOrWhiteSpace(ClientId)
        || !string.IsNullOrWhiteSpace(SessionId)
        || Data.Count > 0;

    public Ga4OrderTracking Clone()
        => new()
        {
            ClientId = ClientId,
            SessionId = SessionId,
            Data = new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase)
        };
}

public sealed class MetaOrderTracking
{
    public string? Fbp { get; set; }
    public string? Fbc { get; set; }
    public Dictionary<string, string?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasData()
        => !string.IsNullOrWhiteSpace(Fbp)
        || !string.IsNullOrWhiteSpace(Fbc)
        || Data.Count > 0;

    public MetaOrderTracking Clone()
        => new()
        {
            Fbp = Fbp,
            Fbc = Fbc,
            Data = new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase)
        };
}
