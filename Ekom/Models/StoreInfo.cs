using Ekom.Interfaces;
using System;

namespace Ekom.Models
{
    public class StoreInfo
    {

        public StoreInfo(IStore store)
        {
            if (store != null)
            {
                Key = store.Key;
                Currency = "";
                Culture = store.Culture.Name;
                Alias = store.Alias;
                VatIncludedInPrice = store.VatIncludedInPrice;
                Vat = store.Vat;
            }

        }

        public Guid Key { get; internal set; }
        public string Currency { get; internal set; }
        public string Culture { get; internal set; }
        public string Alias { get; internal set; }
        public bool VatIncludedInPrice { get; internal set; }
        public decimal Vat { get; internal set; }
    }
}
