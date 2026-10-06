using Ekom.Models;
using System.Text.Json;

namespace Ekom.Analytics;

/// <summary>Resolves a stable identity from persisted data only. Return null for unknown customers.</summary>
public interface IAnalyticsCustomerIdentityResolver
{
    AnalyticsCustomerIdentity? Resolve(OrderData order, JsonElement snapshot);
}

/// <summary>Value must be normalized according to the resolver's identity policy.</summary>
public sealed record AnalyticsCustomerIdentity(string Type, string Value);

public sealed class EmailAnalyticsCustomerIdentityResolver : IAnalyticsCustomerIdentityResolver
{
    public AnalyticsCustomerIdentity? Resolve(OrderData order, JsonElement snapshot)
    {
        ArgumentNullException.ThrowIfNull(order);
        var email = !string.IsNullOrWhiteSpace(order.CustomerEmail)
            ? order.CustomerEmail
            : AnalyticsSnapshotJson.CustomerValue(snapshot, "Email", "customerEmail");
        return string.IsNullOrWhiteSpace(email)
            ? null
            : new AnalyticsCustomerIdentity("email", email.Trim().ToUpperInvariant());
    }
}
