using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Utilities
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        [Obsolete("Use GetValue instead")]
        public static string GetPropertyValue(this Dictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return GetBasePropertyValue(properties, propertyAlias, alias);
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetValue(this Dictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return GetBasePropertyValue(properties, propertyAlias, alias);
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return GetBasePropertyValue(properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), propertyAlias, alias);
        }

        private static string GetBasePropertyValue(Dictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            string val = string.Empty;

            propertyAlias = properties.ContainsKey(propertyAlias + "_" + alias) 
                ? propertyAlias + "_" + alias 
                : properties.ContainsKey(propertyAlias) 
                    ? propertyAlias 
                    : propertyAlias + "_" + System.Globalization.CultureInfo.CurrentCulture.Name;  

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            if (!string.IsNullOrEmpty(alias))
            {
                return val.GetEkomPropertyEditorValue(alias) ?? string.Empty;
            }
            else
            {
                if (string.IsNullOrEmpty(val) && properties.ContainsKey(""))
                {
                    return properties.FirstOrDefault(x => string.IsNullOrEmpty(x.Key)).Value;
                }

                return val ?? string.Empty;
            }
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static bool HasPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            //if (!string.IsNullOrEmpty(language))
            //{
            //    propertyAlias = propertyAlias + "_" + language.ToLowerInvariant();
            //}

            if (properties.ContainsKey(propertyAlias))
            {
                if (!string.IsNullOrEmpty(GetPropertyValue(properties, propertyAlias, alias)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
