using Ekom.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Ekom.Models
{
    public class OrderedShippingProvider
    {
        private ShippingProvider _provider;
        private JObject shippingProviderObject;

        public OrderedShippingProvider(ShippingProvider provider)
        {
            this._provider = provider;

            Id = _provider.Id;
            Key = _provider.Key;
            Title = _provider.Title;
            Price = _provider.Price;
        }

        public OrderedShippingProvider(JObject shippingProviderObject, StoreInfo store)
        {
            this.shippingProviderObject = shippingProviderObject;

            Id = shippingProviderObject["Id"].Value<int>();
            Key = Guid.Parse(shippingProviderObject.GetValue("Key").ToString());
            Title = shippingProviderObject["Title"].Value<string>();
            var orgPrice = shippingProviderObject["Price"]["Value"].Value<decimal>();
            var price = new Price(orgPrice, store);
            Price = price;
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public IDiscountedPrice Price { get; set; }
    }
}
