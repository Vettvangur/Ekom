using Ekom.Models;
using System;

namespace Ekom.Interfaces
{
    public interface IProductDiscountService
    {
        IGlobalDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice);
    }
}
