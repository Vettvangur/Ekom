using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    public class ProductDiscountFactory : IPerStoreFactory<IProductDiscount>
    {
        public IProductDiscount Create(ISearchResult item, IStore store)
        {
            return new GlobalDiscount(item, store);
        }

        public IProductDiscount Create(IContent item, IStore store)
        {
            return new GlobalDiscount(item, store);
        }
    }
}
