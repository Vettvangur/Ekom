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
        IEnumerable<IDomain> Domains { get; set; }
        string OrderNumberPrefix { get; set; }
        string OrderNumberTemplate { get; set; }
        int StoreRootNode { get; set; }
        string Url { get; set; }
        bool VatIncludedInPrice { get; set; }
    }
}
