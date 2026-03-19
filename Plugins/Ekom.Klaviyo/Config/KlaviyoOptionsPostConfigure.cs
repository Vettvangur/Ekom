using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo;

internal sealed class KlaviyoOptionsPostConfigure
    : IPostConfigureOptions<KlaviyoOptions>
{
    public void PostConfigure(string? name, KlaviyoOptions o)
    {
        if (o.Stores is null || o.Stores.Count == 0)
            o.Enabled = false;

        if (string.IsNullOrWhiteSpace(o.Revision))
            o.Enabled = false;

        var needsApi =
            o.Orders.Enabled
            || (o.Catalog.Enabled && o.Catalog.SyncMode == KlaviyoCatalogSyncMode.ApiPush);

        if (needsApi && string.IsNullOrWhiteSpace(o.PrivateApiKey))
            o.Catalog.Enabled = false;

        if (!o.Enabled)
        {
            o.Catalog.Enabled = false;
            o.Orders.Enabled = false;
            o.Subscriptions.Enabled = false;
            o.Tracking.Enabled = false;
            return;
        }

        return;
    }
}
