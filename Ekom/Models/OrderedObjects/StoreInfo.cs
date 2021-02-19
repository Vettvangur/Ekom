using Ekom.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Frozen representation of <see cref="IStore"/>
    /// </summary>
    public class StoreInfo
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonConstructor]
        public StoreInfo(
            Guid key,
            CurrencyModel currency,
            List<CurrencyModel> currencies,
            string culture,
            string alias,
            bool vatIncludedInPrice,
            decimal vat)
        {
            Key = key;
            Culture = culture;
            Alias = alias;
            VatIncludedInPrice = vatIncludedInPrice;
            Vat = vat;

            try
            {
                Currency = currency;
            }
            finally
            {
                if (Currency == null)
                {
                    Currency = new CurrencyModel()
                    {
                        CurrencyFormat = "C",
                        CurrencyValue = "is-IS"
                    };
                }
            }

            try
            {
                Currencies = currencies;
            }
            finally
            {
                if (Currencies == null)
                {
                    Currencies = new List<CurrencyModel>
                    {
                        new CurrencyModel()
                        {
                            CurrencyFormat = "C",
                            CurrencyValue = "is-IS"
                        }
                    };
                }
            }
        }

        public StoreInfo(JObject storeInfoObject)
        {
            try
            {
                Currencies = storeInfoObject["Currencies"]?.ToObject<List<CurrencyModel>>();
            }
            catch
            {
                if (Currencies == null)
                {
                    Currencies = new List<CurrencyModel>
                    {
                        new CurrencyModel()
                        {
                            CurrencyFormat = "C",
                            CurrencyValue = "is-IS"

                        }
                    };
                }
            }

            try
            {
                Currency = storeInfoObject["Currency"]?.ToObject<CurrencyModel>();
            }
            catch
            {
                string currencyCulture = "is-IS";

                if (storeInfoObject["Currency"] != null && !string.IsNullOrEmpty(storeInfoObject["Currency"].Value<string>()))
                {
                    try
                    {
                        currencyCulture = storeInfoObject["Currency"].Value<string>();
                    }
                    catch
                    {

                    }
                }

                Currency = new CurrencyModel()
                {
                    CurrencyFormat = "C",
                    CurrencyValue = currencyCulture
                };
            }

            Key = Guid.Parse(storeInfoObject["Key"].Value<string>());
            Culture = storeInfoObject["Culture"].Value<string>();
            Alias = storeInfoObject["Alias"].Value<string>();
            VatIncludedInPrice = storeInfoObject["VatIncludedInPrice"].Value<bool>();
            Vat = storeInfoObject["Vat"].Value<decimal>();
        }

        public StoreInfo(IStore store)
        {
            if (store != null)
            {
                Key = store.Key;
                Currency = store.Currency;
                Currencies = store.Currencies;
                Culture = store.Culture.Name;
                Alias = store.Alias;
                VatIncludedInPrice = store.VatIncludedInPrice;
                Vat = store.Vat;
            }
        }

        public Guid Key { get; }
        public CurrencyModel Currency { get; set; }
        public List<CurrencyModel> Currencies { get; }
        public string Culture { get; }
        public string Alias { get; }
        public bool VatIncludedInPrice { get; }
        /// <summary>
        /// Stored VAT value: 0.285<para></para>
        /// Effective VAT value: 28.5%<para></para>
        /// </summary>
        public decimal Vat { get; }
    }
}
