using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Utilities
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Get value in properties
        /// </summary>
        /// <param name="alias"></param>
        public static string GetProperty(this Dictionary<string,string> properties, string alias)
        {
            if (properties.Any(x => x.Key == alias))
            {
                return properties.FirstOrDefault(x => x.Key == alias).Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get value in properties
        /// </summary>
        /// <param name="alias"></param>
        public static string GetProperty(this IDictionary<string, string> properties, string alias)
        {
            if (properties.Any(x => x.Key == alias))
            {
                return properties.FirstOrDefault(x => x.Key == alias).Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get value in properties by store
        /// </summary>
        /// <param name="alias"></param>
        public static string GetProperty(this Dictionary<string, string> properties, string alias, string storeAlias)
        {
            if (properties.Any(x => x.Key == alias))
            {
                return properties.FirstOrDefault(x => x.Key == alias).Value.GetVortoValue(storeAlias);
            }

            return string.Empty;
        }
    }
}
