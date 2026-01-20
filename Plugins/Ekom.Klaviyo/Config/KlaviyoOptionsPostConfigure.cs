using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo;

internal sealed class KlaviyoOptionsPostConfigure
    : IPostConfigureOptions<KlaviyoOptions>
{
    public void PostConfigure(string? name, KlaviyoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PrivateApiKey))
        {
            options.Enabled = false;
            return;
        }

        if (options.Stores == null || options.Stores.Count == 0)
        {
            options.Enabled = false;
            return;
        }
    }
}
