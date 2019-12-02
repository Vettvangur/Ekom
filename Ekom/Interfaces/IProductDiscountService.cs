using Ekom.Models;
using System;

namespace Ekom.Interfaces
{
    public interface IProductDiscountService
    {
        IProductDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice);
    }
}
