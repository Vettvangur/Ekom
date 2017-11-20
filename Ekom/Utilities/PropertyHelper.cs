using Our.Umbraco.Vorto.Extensions;
using Our.Umbraco.Vorto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Utilities
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
