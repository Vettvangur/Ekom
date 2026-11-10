using Ekom.Exceptions;

namespace Ekom.Utilities;

/// <summary>
/// Limits on the free-form fields a shopper can attach to an order
/// (customer and shipping properties, provider custom data, order custom data).
/// </summary>
internal static class OrderFieldLimits
{
    /// <summary>Longest field name accepted from a public request.</summary>
    internal const int MaxKeyLength = 100;

    /// <summary>Longest field value accepted from a public request.</summary>
    internal const int MaxValueLength = 4096;

    /// <summary>Most fields one public request may carry.</summary>
    internal const int MaxFieldsPerRequest = 200;

    /// <summary>Most fields one order may hold in each group, across all requests.</summary>
    internal const int MaxFieldsPerOrderGroup = 500;

    /// <summary>
    /// Validates the fields of one public request before they reach the order.
    /// </summary>
    /// <exception cref="CustomerInformationValidationException">A field is too long or there are too many.</exception>
    internal static void ValidateRequestFields(IReadOnlyCollection<KeyValuePair<string, string>>? fields)
    {
        if (fields == null)
        {
            return;
        }

        if (fields.Count > MaxFieldsPerRequest)
        {
            throw new CustomerInformationValidationException($"A request must not contain more than {MaxFieldsPerRequest} fields.");
        }

        foreach (var field in fields)
        {
            if (field.Key.Length > MaxKeyLength)
            {
                throw new CustomerInformationValidationException($"Field names must not exceed {MaxKeyLength} characters.");
            }

            if (field.Value?.Length > MaxValueLength)
            {
                throw new CustomerInformationValidationException($"Field values must not exceed {MaxValueLength} characters.");
            }
        }
    }

    /// <summary>
    /// Ensures a group of order fields would not grow past the per-order cap once
    /// <paramref name="incomingKeys"/> are merged into <paramref name="existing"/>.
    /// </summary>
    internal static void EnsureGroupWithinCap(IReadOnlyDictionary<string, string> existing, IEnumerable<string> incomingKeys, string groupName)
    {
        int total = existing.Count;
        foreach (var key in incomingKeys)
        {
            if (!existing.ContainsKey(key))
            {
                total++;
            }
        }

        if (total > MaxFieldsPerOrderGroup)
        {
            throw new CustomerInformationValidationException($"An order must not hold more than {MaxFieldsPerOrderGroup} {groupName} fields.");
        }
    }
}
