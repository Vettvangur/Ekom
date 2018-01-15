namespace Ekom.Interfaces
{
    public interface IShippingProvider : IPerStoreNodeEntity, IConstrained
    {
        IDiscountedPrice Price { get; }
    }
}
