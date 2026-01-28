using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models;
using Ekom.Models;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

public static class ShippingProviderMapper
{
    public static KlaviyoShippingProvider ToKlaviyoShippingProvider(this OrderedShippingProvider provider)
    {
        return new KlaviyoShippingProvider
        {
            Title = provider.Title,
            Value = provider.Price?.WithVat.Value ?? 0,
            ValueFormatted = provider.Price?.WithVat.CurrencyString ?? string.Empty
        };
    }

    internal static JsonObject ToShippingProviderEvent(this KlaviyoShippingProvider o)
    {
        var obj = new JsonObject
        {
            ["title"] = o.Title,
            ["value"] = o.Value,
            ["value_formatted"] = o.ValueFormatted,
        };

        return obj;
    }
}
