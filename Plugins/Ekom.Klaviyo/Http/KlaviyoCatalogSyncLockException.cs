namespace Ekom.Klaviyo.Http;

/// <summary>
/// Non-transient: Klaviyo blocks Catalog API usage when a feed/catalog sync is active.
/// </summary>
internal sealed class KlaviyoCatalogSyncLockException : KlaviyoApiException
{
    public KlaviyoCatalogSyncLockException(int statusCode, string path, string responseBody, string requestJson)
        : base("Klaviyo denied Catalog API usage because an active Catalog Sync exists.", statusCode, path, responseBody, requestJson)
    {
    }
}
