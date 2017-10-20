using Examine;
using log4net;
using System.Collections.Generic;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// A grouping of countries, used to map payment providers and shipping providers to regions
    /// </summary>
    public class Zone : NodeEntity
    {
        /// <summary>
        /// Countries encompassing this Zone
        /// </summary>
        public IEnumerable<string> Countries => Properties["zone"].Split(',');

        /// <summary>
        /// ctor
        /// </summary>
        public Zone() : base() { }

        /// <summary>
        /// Construct Zone from Examine Search Result
        /// </summary>
        /// <param name="item"></param>
        public Zone(SearchResult item) : base(item) { }

        /// <summary>
        /// Construct zone from umbraco publish event
        /// </summary>
        /// <param name="item"></param>
        public Zone(IContent item) : base(item) { }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
