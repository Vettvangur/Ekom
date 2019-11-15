using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    public class ProductDiscountFactory : IPerStoreFactory<IGlobalDiscount>
    {
        public IGlobalDiscount Create(ISearchResult item, IStore store)
        {
            return new GlobalDiscount(item, store);
        }

        public IGlobalDiscount Create(IContent item, IStore store)
        {
            return new GlobalDiscount(item, store);
        }
    }
}
