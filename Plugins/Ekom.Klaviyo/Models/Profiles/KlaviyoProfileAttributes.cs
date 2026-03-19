namespace Ekom.Klaviyo.Models.Profiles;

public sealed class KlaviyoProfileAttributes
{
    public string? FullName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public string? Organisation { get; set; }

    public IDictionary<string, object?>? CustomProperties { get; set; }
}
