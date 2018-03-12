namespace Ekom.Interfaces
{
    public interface IShippingProvider : IPerStoreNodeEntity, IConstrained
    {
        IPrice Price { get; }
    }
}
