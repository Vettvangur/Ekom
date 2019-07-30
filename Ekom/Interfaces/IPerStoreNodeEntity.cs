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

        /// <summary>
        /// Get value in properties by store
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        string GetPropertyValue(string propAlias, string storeAlias);
    }
}
