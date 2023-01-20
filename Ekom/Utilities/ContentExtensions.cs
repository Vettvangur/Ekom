using Ekom.Models;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Utilities
{
    public static class ContentExtensions
    {
        /// <summary>
        /// Retrieve a store specific property <para/>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <returns>Property Value</returns>
        public static string GetValue(this UmbracoContent item, string field, string storeAlias)
        {
            if (item.Properties.ContainsKey(field))
            {
                var fieldValue = item.Properties.GetPropertyValue(field, storeAlias);

                return fieldValue;
            }

            return string.Empty;
        }

        /// <summary>
        /// Retrieve a price specific property <para/>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="storeAlias"></param>
        /// <param name="currency"></param>
        /// <returns>Property Value</returns>
        public static decimal GetPrice(this UmbracoContent item, string storeAlias, string currency = null)
        {
            var fieldValue = item.GetValue("price", storeAlias);

            if (!string.IsNullOrEmpty(fieldValue))
            {
                var currencyValues = fieldValue.GetCurrencyValues();

                var value = string.IsNullOrEmpty(currency) ? currencyValues.FirstOrDefault() : currencyValues.FirstOrDefault(x => x.Currency == currency);

                return value != null ? value.Value : 0;
            }

            return 0;
        }

        /// <summary>
        /// Determine if an examine item is disabled/unpublished <para />
        /// Traverses up content tree, checking all parents, looks for Umbraco properties matching stores country code
        /// </summary>
        /// <param name="searchResult"></param>
        /// <param name="store">Used to look for umbraco properties matching stores country code </param>
        /// <param name="path"></param>
        /// <param name="allCatalogItems"></param>
        /// <returns>True if disabled</returns>
        public static bool IsItemDisabled(
            this UmbracoContent item,
            IStore store,
            IEnumerable<UmbracoContent> ancestors
            )
        {
            var selfDisableField = item.GetValue("disable", store.Alias);

            if (!string.IsNullOrEmpty(selfDisableField))
            {
                if (selfDisableField.ConvertToBool())
                {
                    return true;
                }
            }

            var catalogAncestors = ancestors.Where(x => x.IsDocumentType("ekmCategory") || x.IsDocumentType("ekmProduct")).ToList();

            foreach (var ancestor in catalogAncestors)
            {
                if (ancestor != null)
                {
                    var disableField = ancestor.GetValue("disable", store.Alias);

                    if (!string.IsNullOrEmpty(disableField))
                    {
                        if (disableField.ConvertToBool())
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
