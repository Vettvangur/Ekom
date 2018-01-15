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

        public Guid Key { get; set; }
        public string Currency { get; set; }
        public string Culture { get; set; }
        public string Alias { get; set; }
        public bool VatIncludedInPrice { get; set; }
        public decimal Vat { get; set; }
    }
}
