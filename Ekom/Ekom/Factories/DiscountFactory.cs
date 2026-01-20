using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Factories
{
    class DiscountFactory : IPerStoreFactory<IDiscount>
    {
        public IDiscount Create(UmbracoContent item, IStore store)
        {
            return new Discount(item, store);
        }
    }
}
