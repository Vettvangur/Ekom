namespace Ekom.Interfaces
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public interface IPerStoreNodeEntity : INodeEntity
    {
        /// <summary>
        /// Ekom Store
        /// </summary>
        IStore Store { get; }
    }
}
