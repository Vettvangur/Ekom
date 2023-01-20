using System.Collections.Generic;

namespace Ekom.Models
{
    /// <summary>
    /// A grouping of countries, used to map payment providers and shipping providers to regions
    /// </summary>
    public interface IZone
    {
        /// <summary>
        /// Countries encompassing this Zone
        /// </summary>
        IEnumerable<string> Countries { get; }

        string Title { get; }
    }
}
