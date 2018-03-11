using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class DiscountFactory : IPerStoreFactory<IDiscount>
    {
        public IDiscount Create(SearchResult item, IStore store)
        {
            return new Discount(item, store);
        }

        public IDiscount Create(IContent item, IStore store)
        {
            return new Discount(item, store);
        }
    }
}
