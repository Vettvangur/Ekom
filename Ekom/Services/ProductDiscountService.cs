using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Services
{
    public class ProductDiscountService : IProductDiscountService
    {
        IPerStoreCache<IProductDiscount> _productDiscountCache;

        internal ProductDiscountService(IPerStoreCache<IProductDiscount> productDiscountCache)
        {
            _productDiscountCache = productDiscountCache;
        }

        
        public ProductDiscount GetProductDiscount(Guid productKey, string storeAlias , string inputPrice )
        {
            var price = decimal.Parse(string.IsNullOrEmpty(inputPrice) ? "0" : inputPrice.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture);

            var applicableDiscounts = new List<ProductDiscount>();

            foreach(var discount in _productDiscountCache.Cache[storeAlias])
            {
                var f = JsonConvert.SerializeObject(discount.Value);
                if (discount.Value.Disabled) 
                {
                    continue;
                }
                if (discount.Value.DiscountItems.Contains(productKey))
                {
                    applicableDiscounts.Add(discount.Value as ProductDiscount);
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
            foreach(var usableDiscount in applicableDiscounts)
            {
                if (usableDiscount.Type == DiscountType.Fixed)
                {
                    if (usableDiscount.StartOfRange > 0 &&  usableDiscount.StartOfRange < price && price < usableDiscount.EndOfRange)
                    {
                        if (usableDiscount.Discount > bestFixedDiscountValue)
                        {
                            bestFixedDiscountValue = usableDiscount.Discount;
                            bestFixedId = usableDiscount.Id;
                        }
                    }
                    
                }
                if (usableDiscount.Type == DiscountType.Percentage)
                {
                    if (usableDiscount.Discount > bestPercentageDiscountValue)
                    {
                        bestPercentageDiscount = usableDiscount.Id;
                        bestPercentageDiscountValue = usableDiscount.Discount;
                    }
                }
            }
            if (bestFixedId == 0)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Id == bestPercentageDiscount);
            }
            else if(bestPercentageDiscount == 0)
            {
                return applicableDiscounts.SingleOrDefault(x => x.Id == bestFixedId);
            }
            else
            {
                var eef = Math.Abs(bestFixedDiscountValue / price) * 100;
                if (Math.Abs(((bestFixedDiscountValue/price)*100)) > bestPercentageDiscount)
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
