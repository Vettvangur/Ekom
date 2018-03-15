using Ekom.Interfaces;
using Newtonsoft.Json.Linq;
using System;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedShippingProvider
    {
        private IShippingProvider _provider;
        private JObject shippingProviderObject;

        public OrderedShippingProvider(IShippingProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

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
        public IPrice Price { get; set; }
    }
}
