using System.Collections.Generic;

namespace Ekom.Utilities
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Get value from umbraco properties
        /// </summary>
        public static string GetPropertyValue(this Dictionary<string, string> properties, string propertyAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetPropertyValue(this Dictionary<string, string> properties, string propertyAlias, string storeAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val.GetVortoValue(storeAlias) ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties
        /// </summary>
        public static string GetPropertyValue(this IDictionary<string, string> properties, string propertyAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetPropertyValue(this IDictionary<string, string> properties, string propertyAlias, string storeAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val.GetVortoValue(storeAlias) ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties
        /// </summary>
        public static string GetPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string storeAlias)
        {
            string val = string.Empty;

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                properties.TryGetValue(propertyAlias, out val);
            }

            return val.GetVortoValue(storeAlias) ?? string.Empty;
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static bool HasPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string storeAlias)
        {

            if (properties.ContainsKey(propertyAlias))
            {
                if (!string.IsNullOrEmpty(GetPropertyValue(properties, propertyAlias, storeAlias)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
