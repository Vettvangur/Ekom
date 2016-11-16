using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Models
{
    public class OrderedProduct
    {
        private int _productId;

        public OrderedProduct(int _productId)
        {
            this._productId = _productId;
        }
    }
}
