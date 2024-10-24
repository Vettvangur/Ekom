using Ekom.Cache;
using Ekom.Models;
using System.Globalization;

namespace Ekom.Services;

class ProductDiscountService
{
    readonly IPerStoreCache<IProductDiscount> _productDiscountCache;
    internal ProductDiscountService(IPerStoreCache<IProductDiscount> productDiscountCache)
    {
        _productDiscountCache = productDiscountCache;
    }

    public virtual IProductDiscount GetProductDiscount(string path, string storeAlias, string inputPrice, string[] categories = null)
    {
        inputPrice = string.IsNullOrEmpty(inputPrice)
            ? "0"
            : inputPrice.Replace(',', '.');

        if (decimal.TryParse(inputPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
        {
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
            
            var bestFixedKey = Guid.Empty;
            var bestPercentageDiscount = Guid.Empty;
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

        return null;
    }
}
