using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Ekom.Utilities;
using System;
using Ekom.Models.Behaviors;

namespace Ekom.Tests.Objects
{
    class CustomDiscount : Discount, IProductDiscount
    {
        public override IConstraints Constraints { get; protected set; }
        public override DiscountType Type { get; }
        public override bool HasMasterStock { get; }

        private decimal _startOfRange = 0;
        public virtual decimal StartOfRange => _startOfRange;

        private decimal _endOfRange = 0;
        public virtual decimal EndOfRange => _endOfRange;

        public bool Disabled => false;

        //public override IReadOnlyCollection<string> Coupons { get; }

        public CustomDiscount(
            IStore store,
            IConstraints constraints,
            decimal amount,
            DiscountType type,
            IEnumerable<string> coupons = null,
            bool hasMasterStock = false
        ) : base(store)
        {
            Constraints = constraints;
            //_properties = amount;
            throw new NotImplementedException("Fix amount");
            HasMasterStock = hasMasterStock;
            //Coupons = coupons?.ToList().AsReadOnly() ?? Enumerable.Empty<string>().ToList().AsReadOnly();
        }

        public CustomDiscount(string json, IStore store) 
            : base(store, json)
        {
            var typeValue = Properties.GetPropertyValue("type");
            if (typeValue == "Percentage")
            {
                Type = DiscountType.Percentage;
            }

            Constraints = new Constraints(this);
        }

        public override IReadOnlyCollection<string> DiscountItems
            => Properties.GetPropertyValue("discountItems")
                    .Split(',')
            // No idea if this makes sense anymore
                    .Select(x => x).ToList();
    }
}
