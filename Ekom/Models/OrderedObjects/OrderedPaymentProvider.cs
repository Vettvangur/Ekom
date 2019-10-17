using Ekom.Interfaces;
using Newtonsoft.Json.Linq;
using System;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Object describing a payment provider at the point a transaction was completed.
    /// </summary>
    public class OrderedPaymentProvider
    {
        private readonly IPaymentProvider _provider;
        private readonly JObject paymentProviderObject;

        public OrderedPaymentProvider(IPaymentProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            Id = _provider.Id;
            Key = _provider.Key;
            Title = _provider.Title;
            Price = _provider.Price;
        }

        public OrderedPaymentProvider(JObject paymentProviderObject, StoreInfo store)
        {
            this.paymentProviderObject = paymentProviderObject;

            Id = paymentProviderObject[nameof(Id)].Value<int>();
            Key = Guid.Parse(paymentProviderObject.GetValue(nameof(Key)).ToString());
            Title = paymentProviderObject[nameof(Title)].Value<string>();
            var orgPrice = paymentProviderObject[nameof(Price)][nameof(Price.Value)].Value<decimal>();
            var price = new Price(orgPrice, store);
            Price = price;
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }

        public IPrice Price { get; set; }
    }
}
