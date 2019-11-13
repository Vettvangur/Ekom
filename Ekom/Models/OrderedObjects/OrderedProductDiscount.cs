using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Currently unused
    /// </summary>
    [Obsolete("Currently unused")]
    public class OrderedProductDiscount
    {
        private readonly string _productDiscountJson;

        [JsonConstructor]
        public OrderedProductDiscount(DiscountType type, decimal discount, decimal startOfRange, decimal endOfRange)
        {
            Type = type;
            Discount = discount;
            StartOfRange = startOfRange;
            EndOfRange = endOfRange;

        }
        public OrderedProductDiscount(GlobalDiscount productDiscount)
        {
            if (productDiscount != null)
            {
                Type = productDiscount.Amount.Type;
                Discount = productDiscount.Amount.Amount;
                StartOfRange = productDiscount.Constraints.StartRange;
                EndOfRange = productDiscount.Constraints.EndRange;
            }
        }

        public DiscountType Type { get; }
        public decimal Discount { get; }
        public decimal StartOfRange { get; }
        public decimal EndOfRange { get; }
        public bool Disabled { get; }
    }
}
