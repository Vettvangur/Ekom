using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Currently unused, <see cref="OrderedDiscount"/> takes care of all persisted discounts
    /// </summary>
    [Obsolete("Currently unused")]
    public class OrderedProductDiscount
    {
        [JsonConstructor]
        public OrderedProductDiscount(DiscountType type, decimal discount, decimal startOfRange, decimal endOfRange, bool disabled)
        {
            Type = type;
            Discount = discount;
            StartOfRange = startOfRange;
            EndOfRange = endOfRange;
            Disabled = disabled;

        }
        public OrderedProductDiscount(ProductDiscount productDiscount)
        {
            if (productDiscount != null)
            {
                Type = productDiscount.Type;
                Discount = productDiscount.Amount;
                StartOfRange = productDiscount.StartOfRange;
                EndOfRange = productDiscount.EndOfRange;
                Disabled = productDiscount.Disabled;
            }
        }

        public DiscountType Type { get; }
        public decimal Discount { get; }
        public decimal StartOfRange { get; }
        public decimal EndOfRange { get; }
        public bool Disabled { get; }
    }
}
