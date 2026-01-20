using Ekom.Models;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Create object of type T
    /// </summary>
    /// <typeparam name="T">Object type factory creates</typeparam>
    public interface IPerStoreFactory<T>
    {
        /// <summary>
        /// Create object from UmbracoContent with explicit store
        /// </summary>
        /// <param name="item">UmbracoContent item</param>
        /// <param name="store"></param>
        /// <returns></returns>
        T Create(UmbracoContent item, IStore store);
    }
}
