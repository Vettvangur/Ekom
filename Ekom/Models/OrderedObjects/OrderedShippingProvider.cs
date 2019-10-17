using Ekom.Interfaces;
using Newtonsoft.Json.Linq;
using System;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedShippingProvider
    {
        private readonly IShippingProvider _provider;
        private readonly JObject shippingProviderObject;

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

            Id = shippingProviderObject[nameof(Id)].Value<int>();
            Key = Guid.Parse(shippingProviderObject.GetValue(nameof(Key)).ToString());
            Title = shippingProviderObject[nameof(Title)].Value<string>();
            var orgPrice = shippingProviderObject[nameof(Price)][nameof(Price.Value)].Value<decimal>();
            var price = new Price(orgPrice, store);
            Price = price;
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public IPrice Price { get; set; }
    }
}
