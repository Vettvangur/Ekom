using Ekom.Models;
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
        /// Umbraco input: 28.5 <para></para>
        /// Stored VAT value: 0.285<para></para>
        /// Effective VAT value: 28.5%<para></para>
        /// </summary>
        decimal Vat { get; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        /// <value>
        /// The culture.
        /// </value>
        CultureInfo Culture { get; }
        string Currency { get; }
        List<CurrencyModel> CurrencyModel { get; }
        string Alias { get; }
        IEnumerable<Models.Domain> Domains { get; }
        string OrderNumberPrefix { get; }
        string OrderNumberTemplate { get; }
        int StoreRootNode { get; }
        string Url { get; }
        bool VatIncludedInPrice { get; }
    }
}
