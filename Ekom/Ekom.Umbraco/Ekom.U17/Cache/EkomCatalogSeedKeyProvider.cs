using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Infrastructure.HybridCache;

namespace Ekom.Umb.Cache;

internal sealed class EkomCatalogSeedKeyProvider : IDocumentSeedKeyProvider
{
    private const string EkomRootContentTypeAlias = "ekom";

    private static readonly string[] CatalogContentTypeAliases =
    [
        "ekmCatalog",
        "ekmStore",
        "ekmProduct",
        "ekmCategory",
        "ekmProductVariant",
        "ekmProductVariantGroup",
    ];

    private readonly IConfiguration _configuration;
    private readonly IDocumentNavigationQueryService _navigationService;
#if UMBRACO_19
    private readonly IDocumentPublishStatusQueryService _publishStatusQueryService;
#else
    private readonly IPublishStatusQueryService _publishStatusQueryService;
#endif

    public EkomCatalogSeedKeyProvider(
        IConfiguration configuration,
        IDocumentNavigationQueryService navigationService,
#if UMBRACO_19
        IDocumentPublishStatusQueryService publishStatusQueryService)
#else
        IPublishStatusQueryService publishStatusQueryService)
#endif
    {
        _configuration = configuration;
        _navigationService = navigationService;
        _publishStatusQueryService = publishStatusQueryService;
    }

    public ISet<Guid> GetSeedKeys()
    {
        if (!_configuration.GetValue<bool>("Ekom:SeedCache"))
        {
            return new HashSet<Guid>();
        }

        var keys = new HashSet<Guid>();
        if (!_navigationService.TryGetRootKeysOfType(EkomRootContentTypeAlias, out var ekomRootKeys))
        {
            return keys;
        }

        foreach (var ekomRootKey in ekomRootKeys)
        {
            if (IsPublishedInAnyCulture(ekomRootKey))
            {
                keys.Add(ekomRootKey);
            }

            foreach (var contentTypeAlias in CatalogContentTypeAliases)
            {
                if (!_navigationService.TryGetDescendantsKeysOfType(ekomRootKey, contentTypeAlias, out var descendantKeys))
                {
                    continue;
                }

                keys.UnionWith(descendantKeys.Where(IsPublishedInAnyCulture));
            }
        }

        return keys;
    }

    private bool IsPublishedInAnyCulture(Guid documentKey)
    {
#if UMBRACO_19
        return _publishStatusQueryService.IsPublishedInAnyCulture(documentKey);
#else
        return _publishStatusQueryService.IsDocumentPublishedInAnyCulture(documentKey);
#endif
    }
}
