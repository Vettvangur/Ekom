using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;

namespace Ekom.Models
{
    public class OrderedShippingProvider
    {
        private readonly IShippingProvider _provider;

        public OrderedShippingProvider(IShippingProvider provider, StoreInfo storeInfo, Dictionary<string, string> customData)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            var dictionary = provider.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Properties = new ReadOnlyDictionary<string, string>(
                dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            StoreInfo = storeInfo;
            Id = _provider.Id;
            Key = _provider.Key;
            Title = _provider.Title;
            Prices = _provider.Prices;
            CustomData = customData;
        }

        public OrderedShippingProvider(JObject shippingProviderObject, StoreInfo storeInfo)
        {
            StoreInfo = storeInfo;

            if (shippingProviderObject.ContainsKey(nameof(Properties)))
            {
                Properties = new ReadOnlyDictionary<string, string>(
                shippingProviderObject[nameof(Properties)].ToObject<Dictionary<string, string>>());
            }
            
            if (shippingProviderObject.ContainsKey(nameof(CustomData)))
            {
                CustomData = new Dictionary<string, string>(
                shippingProviderObject[nameof(CustomData)].ToObject<Dictionary<string, string>>());
            }

            Id = shippingProviderObject["Id"].Value<int>();
            Key = Guid.Parse(shippingProviderObject.GetValue("Key").ToString());
            Title = shippingProviderObject["Title"].Value<string>();

            var pricesObj = shippingProviderObject["Prices"];

            var priceObj = shippingProviderObject["Price"];

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
                            priceObj.ToObject<Price>(EkomJsonDotNet.serializer)
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
            catch (Exception ex)
            {
                //Log.Error("Failed to construct price. ID: " + Id + " Price Object: " + (priceObj != null ? priceObj.ToString() : "Null") + " Prices Object: " + (pricesObj != null ? pricesObj.ToString() : "Null"), ex);
            }
        }
        
        public IReadOnlyDictionary<string, string> Properties;
        private StoreInfo StoreInfo { get; set; }
        public virtual int Id { get; set; }
        public virtual Guid Key { get; set; }
        public virtual string Title { get; set; }
        public virtual IPrice Price
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue);
            }
        }
        public virtual List<IPrice> Prices { get; set; }
        public Dictionary<string, string> CustomData = new Dictionary<string, string>();
    }
}
