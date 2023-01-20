namespace Ekom.Models
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
