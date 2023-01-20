using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Models
{
    /// <summary>
    /// A grouping of countries, used to map payment providers and shipping providers to regions
    /// </summary>
    public class Zone : NodeEntity, IZone
    {
        /// <summary>
        /// Countries encompassing this Zone
        /// </summary>
        public IEnumerable<string> Countries
        {
            get
            {
                try
                {
                    var countries = JsonConvert.DeserializeObject<string[]>(Properties["zoneSelector"]);

                    return countries;

                }
                catch
                {
                    return Properties["zoneSelector"].Split(',');
                }
            }
        }
        /// <summary>
        /// ctor
        /// </summary>
        internal protected Zone() : base() { }

        /// <summary>
        /// Construct Zone
        /// </summary>
        /// <param name="item"></param>
        internal protected Zone(UmbracoContent item) : base(item) { }
    }
}
