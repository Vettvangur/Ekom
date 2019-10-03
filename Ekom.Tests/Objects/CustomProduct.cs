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
        public override ProductDiscount ProductDiscount { get; }

        readonly ProductDiscount pd = new ProductDiscount(null);

        public override IEnumerable<string> Urls { get; internal set; }
        public override IEnumerable<Image> Images => Enumerable.Empty<Image>();
        public CustomProduct(
            string json,
            IStore store,
            string productdisc = null,
            IPrice price = null,
            IEnumerable<string> urls = null
        )
            : base(store)
        {
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (!string.IsNullOrEmpty(productdisc))
            {
                ProductDiscount = new CustomProductDiscount(store, productdisc);
            }

            Price = price ?? new Price(Properties.GetPropertyValue("price", Store.Alias), Store, ProductDiscount == null ? null : new OrderedProductDiscount(ProductDiscount as ProductDiscount));
            var f = Price.WithVat;
            Urls = urls ?? Enumerable.Empty<string>();
        }
    }
}
