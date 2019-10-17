using Ekom.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

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
            List<CurrencyModel> currency,
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
            catch
            {

                //fallback for is-IS

                var list = new List<CurrencyModel>();

                list.Add(new CurrencyModel()
                {
                    CurrencyFormat = "C",
                    CurrencyValue = "is-IS"
                });

                currency = list;
            }


        }

        public StoreInfo(JObject storeInfoObject)
        {



            try
            {
                Currency = storeInfoObject[nameof(Currency)]?.ToObject<List<CurrencyModel>>();
            }
            catch
            {
                var list = new List<CurrencyModel>();

                list.Add(new CurrencyModel()
                {
                    CurrencyFormat = "C",
                    CurrencyValue = "is-IS"
                });

                Currency = list;
            }

            Key = Guid.Parse(storeInfoObject[nameof(Key)].Value<string>());
            Culture = storeInfoObject[nameof(Culture)].Value<string>();
            Alias = storeInfoObject[nameof(Alias)].Value<string>();
            VatIncludedInPrice = storeInfoObject[nameof(VatIncludedInPrice)].Value<bool>();
            Vat = storeInfoObject[nameof(Vat)].Value<int>();

        }

        public StoreInfo(IStore store)
        {
            if (store != null)
            {
                Key = store.Key;
                Currency = store.CurrencyModel;
                Culture = store.Culture.Name;
                Alias = store.Alias;
                VatIncludedInPrice = store.VatIncludedInPrice;
                Vat = store.Vat;
            }
        }

        public Guid Key { get; }
        public List<CurrencyModel> Currency { get; }
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
