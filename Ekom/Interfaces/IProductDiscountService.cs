using Ekom.Models;
using System;

namespace Ekom.Interfaces
{
    public interface IProductDiscountService
    {
        ProductDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice);
    }
}
