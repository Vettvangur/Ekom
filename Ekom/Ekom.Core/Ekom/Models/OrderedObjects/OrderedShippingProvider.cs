using Ekom.API;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;

namespace Ekom.Models;

public class OrderedShippingProvider
{
    private readonly IShippingProvider _provider;

    public OrderedShippingProvider(IShippingProvider provider, StoreInfo storeInfo, Dictionary<string, string>? allData, OrderSettings? orderSettings)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        Dictionary<string, string> dictionary = provider.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var orderDynamic = orderSettings?.OrderDynamicRequest;

        var title = _provider.Title;

        if (orderDynamic != null && !string.IsNullOrEmpty(orderDynamic.Title))
        {
            dictionary["title"] = orderDynamic.Title;
            title = orderDynamic.Title;
        }

        Properties = new ReadOnlyDictionary<string, string>(
           dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

        if (orderDynamic != null && orderDynamic.Prices != null && orderDynamic.Prices.Any() == true)
        {
            Prices = orderDynamic.Prices;
        }
        else
        {
            Prices = _provider.Prices;
        }

        StoreInfo = storeInfo;
        Id = _provider.Id;
        Key = _provider.Key;
        Title = title;
        Method = _provider.Method;
        CustomData = allData?.Where(x => x.Key.StartsWith("customshipping", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    x => x.Key,
                    x => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(x.Value)
                ) ?? new Dictionary<string, string>();
    }

    public OrderedShippingProvider(JObject shippingProviderObject, StoreInfo storeInfo)
    {
        StoreInfo = storeInfo;

        if (shippingProviderObject.ContainsKey(nameof(Properties)))
        {
            Dictionary<string, string>? propertiesObject = shippingProviderObject[nameof(Properties)].ToObject<Dictionary<string, string>>();
            if (propertiesObject != null)
            {
                Properties = new ReadOnlyDictionary<string, string>(propertiesObject);
            }
        }

        if (shippingProviderObject.ContainsKey(nameof(CustomData)))
        {
            Dictionary<string, string>? customDataObject = shippingProviderObject[nameof(CustomData)].ToObject<Dictionary<string, string>>();
            if (customDataObject != null)
            {
                CustomData = new Dictionary<string, string>(customDataObject);
            }
        }

        Id = shippingProviderObject["Id"].Value<int>();
        Key = Guid.Parse(shippingProviderObject.GetValue("Key").ToString());
        Title = shippingProviderObject["Title"].Value<string>();
        Method = ShippingMethods.Pickup;

        if (shippingProviderObject.ContainsKey("Method"))
        {
            var methodValue = shippingProviderObject["Method"]?.Value<string>() ?? "";

            if (!string.IsNullOrEmpty(methodValue))
            {

                if (Enum.TryParse<ShippingMethods>(methodValue, ignoreCase: true, out var method))
                {
                    Method = method;
                }
            }
        }

        JToken? pricesObj = shippingProviderObject["Prices"];

        JToken? priceObj = shippingProviderObject["Price"];

        try
        {
            if (pricesObj != null && !string.IsNullOrEmpty(pricesObj.ToString()))
            {

                Prices = pricesObj.ToString().GetPriceValuesConstructed(storeInfo.Vat, storeInfo.VatIncludedInPrice, storeInfo.Currency);
            }
            else
            {
                try
                {
                    Prices = new List<IPrice>()
                    {
                        priceObj.ToObject<Price>(EkomJsonDotNet.Serializer)
                    };
                }
                catch
                {
                    Prices = new List<IPrice>()
                    {
                        new Price(priceObj, storeInfo.Currency, storeInfo.Vat, storeInfo.VatIncludedInPrice)
                    };
                }
            }
        }
        catch (Exception)
        {
            //Log.Error("Failed to construct price. ID: " + Id + " Price Object: " + (priceObj != null ? priceObj.ToString() : "Null") + " Prices Object: " + (pricesObj != null ? pricesObj.ToString() : "Null"), ex);
        }
    }

    public IReadOnlyDictionary<string, string> Properties;
    private StoreInfo StoreInfo { get; set; }
    public virtual int Id { get; set; }
    public virtual Guid Key { get; set; }
    public virtual string Title { get; set; }
    public virtual ShippingMethods Method { get; set; }
    public virtual IPrice Price
    {
        get
        {

            var match = Prices.FirstOrDefault(
                x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue
            );

            if (match != null)
                return match;

            if (Prices.Any())
            {
                return Prices.First();
            } 

            // Return a fallback 0-price
            return new Price(
                "0",
                StoreInfo.Currency,
                StoreInfo.Vat,
                vatIncludedInPrice: true,
                discount: null
            );
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public virtual List<IPrice> Prices { get; set; } = new List<IPrice>();
    public Dictionary<string, string> CustomData = new Dictionary<string, string>();
}
