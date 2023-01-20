using Ekom.Models;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Create object of type T
    /// </summary>
    /// <typeparam name="T">Object type factory creates</typeparam>
    public interface IObjectFactory<T>
    {
        /// <summary>
        /// Create object from Umbraco Content
        /// </summary>
        /// <param name="item">UmbracoContent item</param>
        /// <returns></returns>
        T Create(UmbracoContent item);
    }
}
