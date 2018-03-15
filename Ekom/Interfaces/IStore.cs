using System.Collections.Generic;
using System.Globalization;
using Umbraco.Core.Models;

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
        string Alias { get; }
        IEnumerable<IDomain> Domains { get; }
        string OrderNumberPrefix { get; }
        string OrderNumberTemplate { get; }
        int StoreRootNode { get; }
        string Url { get; }
        bool VatIncludedInPrice { get; }
    }
}
