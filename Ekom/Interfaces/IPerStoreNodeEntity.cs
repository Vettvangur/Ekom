using Ekom.Interfaces;

namespace Ekom.Models
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public interface IPerStoreNodeEntity : INodeEntity
    {
        /// <summary>
        /// 
        /// </summary>
        new string Title { get; }
    }
}
