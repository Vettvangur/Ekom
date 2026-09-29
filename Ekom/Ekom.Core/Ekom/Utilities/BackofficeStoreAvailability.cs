using Ekom.Models;

namespace Ekom.Utilities;

internal static class BackofficeStoreAvailability
{
    internal static IEnumerable<IStore> FilterEnabledStores(
        UmbracoContent node,
        IEnumerable<UmbracoContent> ancestors,
        IEnumerable<IStore> allStores)
    {
        var folderAlias = node.ContentTypeAlias switch
        {
            "ekmOrderDiscount" => "ekmOrderDiscountsFolder",
            "ekmProductDiscount" => "ekmProductDiscountsFolder",
            "ekmPaymentProvider" => "ekmPaymentProvidersFolder",
            "ekmShippingProvider" => "ekmShippingProvidersFolder",
            _ => null,
        };
        var stores = new List<IStore>();

        foreach (var store in allStores)
        {
            if (folderAlias != null
                ? IsDisabled(node, store.Alias)
                    || ancestors.Any(ancestor => ancestor.ContentTypeAlias == folderAlias
                        && IsDisabled(ancestor, store.Alias))
                : IsCategoryDisabled(node, store.Alias)
                    || ancestors.Any(ancestor => IsCategoryDisabled(ancestor, store.Alias)))
            {
                continue;
            }

            stores.Add(store);
        }

        return stores;
    }

    // Product disable values must remain editable in every category-enabled store.
    private static bool IsCategoryDisabled(UmbracoContent node, string storeAlias) =>
        node.ContentTypeAlias == "ekmCategory"
        && IsDisabled(node, storeAlias);

    private static bool IsDisabled(UmbracoContent node, string storeAlias) =>
        node.Properties.GetValue("disable", storeAlias).IsBoolean();
}
