namespace Ekom.Interfaces
{
    public interface IPaymentProvider : IPerStoreNodeEntity, IConstrained
    {
        string Name { get; }
        IDiscountedPrice Price { get; }
    }
}
