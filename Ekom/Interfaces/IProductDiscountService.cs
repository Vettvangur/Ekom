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
        IProductDiscount GetProductDiscount(string path, string storeAlias, string inputPrice, string[] categories = null);
    }
}
