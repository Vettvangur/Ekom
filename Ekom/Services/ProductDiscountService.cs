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
        readonly IPerStoreCache<IProductDiscount> _productDiscountCache;

        internal ProductDiscountService(IPerStoreCache<IProductDiscount> productDiscountCache)
        {
            _productDiscountCache = productDiscountCache;
        }

        public IProductDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice)
        {
            var price = decimal.Parse(string.IsNullOrEmpty(inputPrice) ? "0" : inputPrice.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);

            var applicableDiscounts = new List<IProductDiscount>();

            foreach (var discount in _productDiscountCache.Cache[storeAlias])
            {
                if (discount.Value.DiscountItems.Contains(productKey))
                {
                    applicableDiscounts.Add(discount.Value);
                }
            }

            Guid bestFixedKey = Guid.Empty;
            Guid bestPercentageDiscount = Guid.Empty;
            decimal bestPercentageDiscountValue = 0;
            decimal bestFixedDiscountValue = 0;

            if (applicableDiscounts.Count() == 0)
            {
                return null;
            }

            foreach (var usableDiscount in applicableDiscounts)
            {
                if (usableDiscount.Type == DiscountType.Fixed)
                {
                    if (usableDiscount.Constraints.StartRange < price 
                    && usableDiscount.Constraints.EndRange > 0 && price < usableDiscount.Constraints.EndRange)
                    {
                        if (usableDiscount.Amount > bestFixedDiscountValue)
                        {
                            bestFixedDiscountValue = usableDiscount.Amount;
                            bestFixedKey = usableDiscount.Key;
                        }
                    }
                }
                if (usableDiscount.Type == DiscountType.Percentage)
                {
                    if (usableDiscount.Amount > bestPercentageDiscountValue)
                    {
                        bestPercentageDiscount = usableDiscount.Key;
                        bestPercentageDiscountValue = usableDiscount.Amount;
                    }
                }
            }
            if (bestFixedKey == Guid.Empty)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Key == bestPercentageDiscount);
            }
            else if (bestPercentageDiscount == Guid.Empty)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Key == bestFixedKey);
            }
            else
            {
                var eef = Math.Abs(bestFixedDiscountValue / price) * 100;
                if (Math.Abs(((bestFixedDiscountValue / price) * 100)) > bestPercentageDiscountValue)
                {
                    return applicableDiscounts.SingleOrDefault(x => x.Key == bestFixedKey);
                }
                else
                {
                    return applicableDiscounts.SingleOrDefault(x => x.Key == bestPercentageDiscount);
                }
            }
        }
    }
}
