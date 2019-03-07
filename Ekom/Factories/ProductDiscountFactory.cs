using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    public class ProductDiscountFactory : IPerStoreFactory<IProductDiscount>
    {
        public IProductDiscount Create(SearchResult item, IStore store)
        {
            return new ProductDiscount(item, store);
        }

        public IProductDiscount Create(IContent item, IStore store)
        {
            return new ProductDiscount(item, store);
        }
    }
}
