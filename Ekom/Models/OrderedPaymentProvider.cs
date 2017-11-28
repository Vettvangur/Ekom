using Ekom.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Ekom.Models
{
    public class OrderedPaymentProvider
    {
        private PaymentProvider _provider;
        private JObject paymentProviderObject;

        public OrderedPaymentProvider(PaymentProvider provider)
        {
            this._provider = provider;
            Id = _provider.Id;
            Key = _provider.Key;
            Title = _provider.Title;
            Price = _provider.Price;
        }

        public OrderedPaymentProvider(JObject paymentProviderObject, StoreInfo store)
        {
            this.paymentProviderObject = paymentProviderObject;

            Id = paymentProviderObject["Id"].Value<int>();
            Key = Guid.Parse(paymentProviderObject.GetValue("Key").ToString());
            Title = paymentProviderObject["Title"].Value<string>();
            var orgPrice = paymentProviderObject["Price"]["Value"].Value<decimal>();
            var price = new Price(orgPrice, store);
            Price = price;
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }

        public IDiscountedPrice Price { get; set; }
    }
}
