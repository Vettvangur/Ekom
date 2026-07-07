namespace Ekom.Models
{
    public interface IShippingProvider : IPerStoreNodeEntity, IConstrained
    {
        IPrice Price { get; }

        List<IPrice> Prices { get; }
        ShippingMethods Method { get; }
    }
}
