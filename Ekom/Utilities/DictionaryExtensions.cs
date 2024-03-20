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
            return System.Web.HttpUtility.HtmlDecode(GetBasePropertyValue(properties, propertyAlias, alias));
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetValue(this Dictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return System.Web.HttpUtility.HtmlDecode(GetBasePropertyValue(properties, propertyAlias, alias));
        }

        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return System.Web.HttpUtility.HtmlDecode(GetBasePropertyValue(properties, propertyAlias, alias));
        }


        /// <summary>
        /// Get value from umbraco properties by store
        /// Retrieves a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        public static string GetPropertyValue(this IReadOnlyDictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            return System.Web.HttpUtility.HtmlDecode(GetBasePropertyValue(properties, propertyAlias, alias));
        }

        private static string GetBasePropertyValue(IReadOnlyDictionary<string, string> properties, string propertyAlias, string alias = null)
        {
            string val = string.Empty;
            string modifiedPropertyAlias = propertyAlias;

            // Build the modified property alias based on the available keys
            if (!string.IsNullOrEmpty(alias))
            {
                var tempAlias = propertyAlias + "_" + alias;
                if (properties.ContainsKey(tempAlias))
                {
                    modifiedPropertyAlias = tempAlias;
                }
            }

            var tempCultureAlias = propertyAlias + "_" + System.Globalization.CultureInfo.CurrentCulture.Name;
            if (properties.ContainsKey(tempCultureAlias))
            {
                modifiedPropertyAlias = tempCultureAlias;
            }

            // Attempt to retrieve the value for the final property alias
            properties.TryGetValue(modifiedPropertyAlias, out val);

            // Special processing based on alias
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
