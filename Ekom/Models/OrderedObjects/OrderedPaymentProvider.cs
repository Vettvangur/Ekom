using Ekom.JsonDotNet;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ekom.Models
{
    /// <summary>
    /// Object describing a payment provider at the point a transaction was completed.
    /// </summary>
    public class OrderedPaymentProvider
    {
        private readonly IPaymentProvider _provider;
        private readonly JObject paymentProviderObject;

        public OrderedPaymentProvider(IPaymentProvider provider, StoreInfo storeInfo)
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
        }

        public OrderedPaymentProvider(JObject paymentProviderObject, StoreInfo storeInfo)
        {
            this.paymentProviderObject = paymentProviderObject;

            if (paymentProviderObject.ContainsKey(nameof(Properties)))
            {
                Properties = new ReadOnlyDictionary<string, string>(
                paymentProviderObject[nameof(Properties)].ToObject<Dictionary<string, string>>());
            }

            StoreInfo = storeInfo;
            Id = paymentProviderObject["Id"].Value<int>();
            Key = Guid.Parse(paymentProviderObject.GetValue("Key").ToString());
            Title = paymentProviderObject["Title"].Value<string>();

            var pricesObj = paymentProviderObject["Prices"];

            var priceObj = paymentProviderObject["Price"];

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
    }
}
