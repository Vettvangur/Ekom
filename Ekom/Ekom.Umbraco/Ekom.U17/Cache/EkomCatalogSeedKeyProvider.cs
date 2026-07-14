using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.HybridCache;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb.Cache;

internal sealed class EkomCatalogSeedKeyProvider : IDocumentSeedKeyProvider
{
    private static readonly string[] CatalogContentTypeAliases =
    [
        "ekmProduct",
        "ekmCategory",
        "ekmProductVariant",
        "ekmProductVariantGroup",
    ];

    private const int PageSize = 2_000;

    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IConfiguration _configuration;
    private readonly IScopeProvider _scopeProvider;

    public EkomCatalogSeedKeyProvider(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IConfiguration configuration,
        IScopeProvider scopeProvider)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _configuration = configuration;
        _scopeProvider = scopeProvider;
    }

    public ISet<Guid> GetSeedKeys()
    {
        if (!_configuration.GetValue<bool>("Ekom:SeedCache"))
        {
            return new HashSet<Guid>();
        }

        var keys = new HashSet<Guid>();
        var filter = new Query<IContent>(_scopeProvider.SqlContext).Where(x => !x.Trashed);

        foreach (var contentTypeAlias in CatalogContentTypeAliases)
        {
            var contentType = _contentTypeService.Get(contentTypeAlias);
            if (contentType == null)
            {
                continue;
            }

            var pageIndex = 0;
            long totalRecords;

            do
            {
                var content = _contentService.GetPagedOfType(
                    contentType.Id,
                    pageIndex,
                    PageSize,
                    out totalRecords,
                    filter);

                keys.UnionWith(content.Where(x => x.Published).Select(x => x.Key));
                pageIndex++;
            }
            while (pageIndex * PageSize < totalRecords);
        }

        return keys;
    }
}
