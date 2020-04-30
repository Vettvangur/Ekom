using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IShippingProvider : IPerStoreNodeEntity, IConstrained
    {
        IPrice Price { get; }

        List<IPrice> Prices { get; }
    }
}
