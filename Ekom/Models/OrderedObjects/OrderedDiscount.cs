using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedDiscount
    {
        public OrderedDiscount()
        {

        }

        public OrderedDiscount(IDiscount discount)
        {
            Key = discount.Key;
            Amount = discount.Amount;
            Constraints = new OrderedConstraints(discount.Constraints);
            Coupons = new List<string>(discount.Coupons);
            HasMasterStock = discount.HasMasterStock;
        }

        public Guid Key { get; }

        public DiscountAmount Amount { get; }

        [JsonIgnore]
        public IConstraints Constraints { get; internal set; }

        public IReadOnlyCollection<string> Coupons { get; }

        public bool HasMasterStock { get; }

        public int CompareTo(IDiscount other)
        {
            if (other == null)
                return 1;

            else if (Amount.Type != other.Amount.Type)
                throw new FormatException("Discounts are not equal, please compare type before comparing value.");
            else if (Amount.Amount == other.Amount.Amount)
                return 0;
            else if (Amount.Amount > other.Amount.Amount)
                return 1;
            else
                return -1;
        }
    }
}
