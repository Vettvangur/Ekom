using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface IProductDiscountService
    {
        ProductDiscount GetProductDiscount(Guid productKey, string storeAlias, string inputPrice);
    }
}
