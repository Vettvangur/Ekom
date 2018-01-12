using System.Collections.Generic;
using System.Linq;

namespace Ekom.Utilities
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Get value in properties
        /// </summary>
        public static string GetPropertyValue(this Dictionary<string, string> properties, string propertyAlias)
        {
            string val = null;
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val;
        }

        /// <summary>
        /// Get value in properties
        /// </summary>
        public static string GetPropertyValue(this IDictionary<string, string> properties, string propertyAlias)
        {
            string val = null;
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val;
        }

        /// <summary>
        /// Get value in properties by store
        /// </summary>
        public static string GetPropertyValue(this Dictionary<string, string> properties, string propertyAlias, string storeAlias)
        {
            string val = null;
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val?.GetVortoValue(storeAlias);
        }
    }
}
