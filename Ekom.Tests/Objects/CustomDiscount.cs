using Ekom.Interfaces;
using Ekom.Models.Discounts;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Tests.Objects
{
    class CustomDiscount : Discount
    {
        public override IConstraints Constraints { get; protected set; }
        public override DiscountAmount Amount { get; protected set; }
        public override bool HasMasterStock { get; }

        public override IReadOnlyCollection<string> Coupons { get; }

        public CustomDiscount(
            IStore store,
            IConstraints constraints,
            DiscountAmount amount,
            IEnumerable<string> coupons = null,
            bool hasMasterStock = false
        ) : base(store)
        {
            Constraints = constraints;
            Amount = amount;
            HasMasterStock = hasMasterStock;
            Coupons = coupons.ToList().AsReadOnly();
        }
    }
}
