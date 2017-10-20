using System.Globalization;

namespace Ekom.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IStore : INodeEntity
    {
        /// <summary>
        /// Gets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        decimal Vat { get; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        /// <value>
        /// The culture.
        /// </value>
        CultureInfo Culture { get; }
    }
}
