using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Tests.Objects
{
    class CustomProduct : Product
    {
        public override IPrice Price { get; }

        public override IEnumerable<string> Urls { get; internal set; }
        public CustomProduct(
            string json,
            IStore store,
            IPrice price = null,
            IEnumerable<string> urls = null
        )
            : base(store)
        {
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            Price = price ?? new Price(
                Properties.GetPropertyValue("price", Store.Alias),
                Store.Currency,
                Store.Vat,
                Store.VatIncludedInPrice);

            var discount = ProductDiscount();

            if (price == null && discount != null)
            {
                Price = new Price(
                    Properties.GetPropertyValue("price", Store.Alias),
                    Store.Currency,
                    Store.Vat,
                    Store.VatIncludedInPrice,
                    new OrderedDiscount(discount));
            }

            Urls = urls ?? Enumerable.Empty<string>();
        }
    }
}
