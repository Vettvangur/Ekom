using Umbraco.Extensions;

namespace Ekom.Klaviyo.Helpers;

internal static class KlaviyoListIdResolver
{
    public static string? ResolveSubscriptionListId(this KlaviyoOptions options, string storeAlias, string? listId)
    {
        if (!string.IsNullOrWhiteSpace(listId))
            return listId;

        var storeListId = options.Stores
            .FirstOrDefault(x => x.Alias.InvariantEquals(storeAlias))
            ?.ListId;

        if (!string.IsNullOrWhiteSpace(storeListId))
            return storeListId;

        return options.Subscriptions.DefaultListId;
    }
}
