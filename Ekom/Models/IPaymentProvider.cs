using System.Collections.Generic;

namespace Ekom.Models
{
    public interface IPaymentProvider : IPerStoreNodeEntity, IConstrained
    {
        string Name { get; }
        IPrice Price { get; }
        string Description { get; }
        List<IPrice> Prices { get; }
    }
}
