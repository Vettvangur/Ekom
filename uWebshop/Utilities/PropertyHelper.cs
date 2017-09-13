using System.Collections.Generic;

namespace uWebshop.Utilities
{
    /// <summary>
    /// Get property values from a dictionary while failing silently
    /// </summary>
    static class PropertyHelper
    {
        /// <summary>
        /// Get property values from a dictionary while failing silently
        /// </summary>
        /// <param name="dic"></param>
        /// <param name="propertyAlias">Dictionary key</param>
        public static string GetPropertyValue(this Dictionary<string, string> dic, string propertyAlias)
        {
            string val = null;
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                dic.TryGetValue(propertyAlias, out val);
            }

            return val;
        }
    }
}
