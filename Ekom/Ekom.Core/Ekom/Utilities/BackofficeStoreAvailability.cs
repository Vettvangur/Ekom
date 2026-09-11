using Ekom.Models;

namespace Ekom.Utilities;

internal static class BackofficeStoreAvailability
{
    internal static IEnumerable<IStore> FilterEnabledStores(
        UmbracoContent node,
        IEnumerable<UmbracoContent> ancestors,
        IEnumerable<IStore> allStores)
    {
        var stores = new List<IStore>();

        foreach (var store in allStores)
        {
            if (IsCategoryDisabled(node, store.Alias)
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
        && node.Properties.GetValue("disable", storeAlias).IsBoolean();
}
