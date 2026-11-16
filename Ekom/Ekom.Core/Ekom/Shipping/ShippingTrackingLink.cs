namespace Ekom.Shipping;

/// <summary>Builds the address where a shipment can be followed from a carrier account's template.</summary>
public static class ShippingTrackingLink
{
    /// <summary>The placeholder a template uses for the tracking number.</summary>
    public const string Placeholder = "{trackingNumber}";

    /// <summary>
    /// Fills <see cref="Placeholder"/> in the template with the tracking number.
    /// Null when either is missing or the result is not an http or https address.
    /// </summary>
    public static string? Format(string? template, string? trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            return null;
        }

        var address = template.Replace(Placeholder, Uri.EscapeDataString(trackingNumber.Trim()), StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? address
            : null;
    }
}
