using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ekom.Services
{
    class ProductDiscountService : IProductDiscountService
    {
        readonly IPerStoreCache<IProductDiscount> _productDiscountCache;

        internal ProductDiscountService(IPerStoreCache<IProductDiscount> productDiscountCache)
        {
            _productDiscountCache = productDiscountCache;
        }

        public IProductDiscount GetProductDiscount(string path, string storeAlias, string inputPrice, string[] categories = null)
        {
            var price = decimal.Parse(string.IsNullOrEmpty(inputPrice) 
                ? "0" 
                : inputPrice.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);

            var applicableDiscounts = new List<IProductDiscount>();

            // If no discounts are available in cache
            if (!_productDiscountCache.Cache[storeAlias].Values.Any())
            {
                return null;
            }

            foreach (var discount in _productDiscountCache.Cache[storeAlias])
            {
                if (discount.Value.Disabled)
                {
                    continue;
                }

                var disc = discount.Value as Discount;

                if (!string.IsNullOrEmpty(path) 
                && path.Split(',').Intersect(disc.DiscountItems).Any() 
                || (categories != null && categories.Intersect(disc.DiscountItems).Any()))
                {
                    applicableDiscounts.Add(discount.Value);
                }
            }

            // If no discounts are available for this item
            if (applicableDiscounts.Count == 0)
            {
                return null;
            }

            Guid bestFixedKey = Guid.Empty;
            Guid bestPercentageDiscount = Guid.Empty;
            decimal bestPercentageDiscountValue = 0;
            decimal bestFixedDiscountValue = 0;



            foreach (var usableDiscount in applicableDiscounts)
            {
                if (usableDiscount.Type == DiscountType.Fixed)
                {
                    if (usableDiscount.StartOfRange < price 
                    && (usableDiscount.EndOfRange == 0 || price < usableDiscount.EndOfRange))
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
