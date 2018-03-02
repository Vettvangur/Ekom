using Ekom.Interfaces;
using Newtonsoft.Json.Linq;

namespace Ekom.Models.OrderedObjects
{
    class OrderedPrice : IPrice
    {
        public OrderedPrice(IPrice price)
        {
            OriginalValue = price.OriginalValue;
            Value = price.Value;

            Vat = price.Vat;

            BeforeDiscount = price.BeforeDiscount;
            WithVat = price.WithVat;
            WithoutVat = price.WithoutVat;

            // OrderedDiscounts are never modified
            Discount = price.Discount;
        }

        public OrderedPrice(JToken orderedPriceObj)
        {
            var priceObj = JObject.Parse(orderedPriceObj.ToString());

            OriginalValue = priceObj.Value<decimal>("OriginalValue");
            Value = priceObj.Value<decimal>("Value");
            Vat = priceObj.Value<decimal>("Vat");

            var orderedDiscount = priceObj["Discount"].ToObject<OrderedDiscount>();
            if (orderedDiscount != null)
            {
                orderedDiscount.Constraints = priceObj["Discount"]["Constraints"].ToObject<OrderedConstraints>();
            }
            Discount = orderedDiscount;

            BeforeDiscount = priceObj["BeforeDiscount"].ToObject<OrderedCalculatedPrice>();
            WithVat = priceObj["WithVat"].ToObject<OrderedCalculatedPrice>();
            WithoutVat = priceObj["WithoutVat"].ToObject<OrderedCalculatedPrice>();
        }

        public decimal OriginalValue { get; }

        public ICalculatedPrice BeforeDiscount { get; }

        public decimal Value { get; }

        public OrderedDiscount Discount { get; }

        public ICalculatedPrice WithVat { get; }

        public ICalculatedPrice WithoutVat { get; }

        public decimal Vat { get; }

        public object Clone() => MemberwiseClone();
    }
}
