using Ekom.Cache;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Utilities;

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
            string fieldValue = item.Properties.GetPropertyValue(field, storeAlias);

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
    public static decimal GetPrice(this UmbracoContent item, string storeAlias, string? currency = null)
    {
        string fieldValue = item.GetValue("price", storeAlias);

        if (!string.IsNullOrEmpty(fieldValue))
        {
            List<CurrencyValue> currencyValues = fieldValue.GetCurrencyValues(storeAlias);

            CurrencyValue? value = string.IsNullOrEmpty(currency) ? currencyValues.FirstOrDefault() : currencyValues.FirstOrDefault(x => x.Currency == currency);

            return value != null ? value.Value : 0;
        }

        return 0;
    }

    /// <summary>
    /// Determine if an node is disabled/unpublished <para />
    /// Traverses up content tree, checking all parents, looks for Umbraco properties matching stores country code
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store"></param>
    /// <param name="ancestors"></param>
    /// <returns>True if disabled</returns>
    public static bool IsItemDisabled(
        this UmbracoContent item,
        IStore store,
        IEnumerable<UmbracoContent> ancestors
        )
    {
        if (ancestors == null)
        {
            return true;
        }

        string selfDisableField = item.GetValue("disable", store.Alias);

        if (!string.IsNullOrEmpty(selfDisableField))
        {
            if (selfDisableField.ConvertToBool())
            {
                return true;
            }
        }

        if (item.ContentTypeAlias is not ("ekmProduct" or "ekmCategory" or "ekmProductVariantGroup"
            or "ekmProductVariant")) return false;

        List<UmbracoContent> catalogAncestors = ancestors.Where(x => x.IsDocumentType("ekmCategory") || x.IsDocumentType("ekmProduct")).ToList();

        foreach (UmbracoContent? ancestor in catalogAncestors)
        {
            if (ancestor != null)
            {
                string disableField = ancestor.GetValue("disable", store.Alias);

                if (string.IsNullOrEmpty(disableField)) continue;

                if (disableField.ConvertToBool())
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determine if a content item is disabled<para />
    /// Traverses up content tree, checking all parents, looks for Umbraco properties matching stores code
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store">Used to look for umbraco properties matching stores code </param>
    /// <returns>True if disabled</returns>
    public static bool IsItemDisabled(
        this UmbracoContent item,
        IStore store
        )
    {
        string selfDisableField = item.GetValue("disable", store.Alias);

        if (!string.IsNullOrEmpty(selfDisableField))
        {
            if (selfDisableField.ConvertToBool())
            {
                return true;
            }
        }

        if (item.Level > 3)
        {
            var skipCount = 3;
            var pathArray = item.Path.Split(',');

            var takeCountByAlias = new Dictionary<string, int>
            {
                ["ekmProduct"] = pathArray.Length - 4,
                ["ekmCategory"] = pathArray.Length - 4,
                ["ekmProductVariantGroup"] = pathArray.Length - 5,
                ["ekmProductVariant"] = pathArray.Length - 6
            };

            if (takeCountByAlias.TryGetValue(item.ContentTypeAlias, out int takeCount))
            {
                var paths = pathArray.Skip(skipCount).Take(takeCount);
                var categoryCache = Configuration.Resolver.GetService<IPerStoreIndexedCache<ICategory>>();

                foreach (var pathId in paths)
                {
                    if (!int.TryParse(pathId, out int id)) continue;

                    if (categoryCache == null || !categoryCache.TryGetById(store.Alias, id, out var category) || category == null)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
