using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedProductDiscount
    {
        private string _productDiscountJson;

        [JsonConstructor]
        public OrderedProductDiscount(DiscountType type, decimal discount,  decimal startOfRange, decimal endOfRange, bool disabled, Store store) 
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
                Discount = productDiscount.Discount;
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
