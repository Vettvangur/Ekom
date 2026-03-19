using Ekom.Klaviyo.Models.Orders;
using Ekom.Models;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

public static class PaymentProviderMapper
{
    public static KlaviyoPaymentProvider ToKlaviyoPaymentProvider(this OrderedPaymentProvider provider)
    {
        return new KlaviyoPaymentProvider
        {
            Title = provider.Title,
            Value = provider.Price?.WithVat.Value ?? 0,
            ValueFormatted = provider.Price?.WithVat.CurrencyString ?? string.Empty
        };
    }


    internal static JsonObject ToPaymentProviderEvent(this KlaviyoPaymentProvider o)
    {
        var obj = new JsonObject
        {
            ["title"] = o.Title,
            ["value"] = o.Value
        };

        return obj;
    }

}
