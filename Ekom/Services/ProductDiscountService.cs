using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ekom.Services
{
    public class ProductDiscountService : IProductDiscountService
    {
        readonly IPerStoreCache<IGlobalDiscount> _productDiscountCache;

        internal ProductDiscountService(IPerStoreCache<IGlobalDiscount> productDiscountCache)
        {
            _productDiscountCache = productDiscountCache;
        }

        public IGlobalDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice)
        {
            var price = decimal.Parse(string.IsNullOrEmpty(inputPrice) ? "0" : inputPrice.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);

            var applicableDiscounts = new List<GlobalDiscount>();

            foreach (var discount in _productDiscountCache.Cache[storeAlias])
            {
                if (discount.Value.DiscountItems.Contains(productKey))
                {
                    applicableDiscounts.Add(discount.Value as GlobalDiscount);
                }
            }

            int bestFixedId = 0;
            int bestPercentageDiscount = 0;
            decimal bestPercentageDiscountValue = 0;
            decimal bestFixedDiscountValue = 0;
            if (applicableDiscounts.Count() == 0)
            {
                return null;
            }
            if (applicableDiscounts.Count() == 1)
            {
                return applicableDiscounts.First();
            }
            foreach (var usableDiscount in applicableDiscounts)
            {
                if (usableDiscount.Amount.Type == DiscountType.Fixed)
                {
                    if (usableDiscount.Constraints.StartRange < price 
                    && usableDiscount.Constraints.EndRange > 0 && price < usableDiscount.Constraints.EndRange)
                    {
                        if (usableDiscount.Amount.Amount > bestFixedDiscountValue)
                        {
                            bestFixedDiscountValue = usableDiscount.Amount.Amount;
                            bestFixedId = usableDiscount.Id;
                        }
                    }
                }
                if (usableDiscount.Amount.Type == DiscountType.Percentage)
                {
                    if (usableDiscount.Amount.Amount > bestPercentageDiscountValue)
                    {
                        bestPercentageDiscount = usableDiscount.Id;
                        bestPercentageDiscountValue = usableDiscount.Amount.Amount;
                    }
                }
            }
            if (bestFixedId == 0)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Id == bestPercentageDiscount);
            }
            else if (bestPercentageDiscount == 0)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Id == bestFixedId);
            }
            else
            {
                var eef = Math.Abs(bestFixedDiscountValue / price) * 100;
                if (Math.Abs(((bestFixedDiscountValue / price) * 100)) > bestPercentageDiscount)
                {
                    return applicableDiscounts.SingleOrDefault(x => x.Id == bestFixedId);
                }
                else
                {
                    return applicableDiscounts.SingleOrDefault(x => x.Id == bestPercentageDiscount);
                }
            }
        }
    }
}
