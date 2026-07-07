namespace Ekom.Klaviyo.Helpers;

internal static class KlaviyoListIdResolver
{
    public static string? ResolveSubscriptionListId(this KlaviyoOptions options, string storeAlias, string? listId)
    {
        if (!string.IsNullOrWhiteSpace(listId))
            return listId;

        var storeListId = options.Stores
            .FirstOrDefault(x => string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase))
            ?.ListId;

        if (!string.IsNullOrWhiteSpace(storeListId))
            return storeListId;

        return options.Subscriptions.DefaultListId;
    }
}
