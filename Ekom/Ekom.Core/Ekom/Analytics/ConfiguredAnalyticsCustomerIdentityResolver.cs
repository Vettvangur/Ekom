using Ekom.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace Ekom.Analytics;

/// <summary>Resolves only the configured identifier from persisted order data.</summary>
public sealed class ConfiguredAnalyticsCustomerIdentityResolver : IAnalyticsCustomerIdentityResolver
{
    private const string PropertyPrefix = "Property:";
    private readonly EmailAnalyticsCustomerIdentityResolver _emailResolver = new();
    private readonly IOptions<AnalyticsOptions> _options;

    public ConfiguredAnalyticsCustomerIdentityResolver(IOptions<AnalyticsOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    internal static bool IsValidSelector(string? selector) =>
        string.Equals(selector, "CustomerEmail", StringComparison.OrdinalIgnoreCase)
        || string.Equals(selector, "CustomerUsername", StringComparison.OrdinalIgnoreCase)
        || string.Equals(selector, "CustomerId", StringComparison.OrdinalIgnoreCase)
        || (selector != null && selector.StartsWith(PropertyPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(selector[PropertyPrefix.Length..]));

    public AnalyticsCustomerIdentity? Resolve(OrderData order, JsonElement snapshot)
    {
        ArgumentNullException.ThrowIfNull(order);
        var selector = _options.Value.CustomerIdentifier;
        if (!IsValidSelector(selector)) return null;

        if (string.Equals(selector, "CustomerEmail", StringComparison.OrdinalIgnoreCase))
            return _emailResolver.Resolve(order, snapshot);

        if (string.Equals(selector, "CustomerUsername", StringComparison.OrdinalIgnoreCase))
        {
            var username = !string.IsNullOrWhiteSpace(order.CustomerUsername) ? order.CustomerUsername
                : AnalyticsSnapshotJson.CustomerValue(snapshot, "Username", "customerUsername");
            return string.IsNullOrWhiteSpace(username) ? null
                : new AnalyticsCustomerIdentity("username", username.Trim().ToUpperInvariant());
        }

        var customer = AnalyticsSnapshotJson.Property(AnalyticsSnapshotJson.Property(snapshot, "CustomerInformation"), "Customer");
        if (string.Equals(selector, "CustomerId", StringComparison.OrdinalIgnoreCase))
        {
            var id = order.CustomerId > 0 ? order.CustomerId : AnalyticsSnapshotJson.IntValue(customer, "UserId");
            return id > 0 ? new AnalyticsCustomerIdentity("customer-id", id.Value.ToString(CultureInfo.InvariantCulture)) : null;
        }

        var alias = selector[PropertyPrefix.Length..];
        var properties = AnalyticsSnapshotJson.Property(customer, "Properties");
        // Dictionary aliases are exact keys, not case-insensitive field names or JSON paths.
        if (properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty(alias, out var saved)
            || saved.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(saved.GetString()))
            return null;

        var value = saved.GetString()!.Trim();
        // Keep the entire alias in the hash input without widening deployed type columns.
        // Also encode whitespace/control aliases so mapper trimming and hash separators cannot conflate keys.
        return alias.Length > 24 || alias != alias.Trim() || alias.Any(char.IsControl)
            ? new AnalyticsCustomerIdentity("property", alias.Length.ToString(CultureInfo.InvariantCulture) + ":" + alias + value)
            : new AnalyticsCustomerIdentity("property:" + alias, value);
    }
}
