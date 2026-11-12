using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;

namespace Ekom.Umb;

internal static class ContentServiceExtensions
{
    /// <summary>
    /// Pages the children of a content node with all properties loaded, across the
    /// <see cref="IContentService.GetPagedChildren"/> signatures of the supported Umbraco versions.
    /// </summary>
    public static IEnumerable<IContent> GetPagedChildNodes(
        this IContentService contentService,
        int parentId,
        long pageIndex,
        int pageSize,
        out long totalRecords,
        IQuery<IContent>? filter = null,
        Ordering? ordering = null)
    {
#if UMBRACO_19
        return contentService.GetPagedChildren(parentId, pageIndex, pageSize, out totalRecords, null, filter, ordering);
#else
        return contentService.GetPagedChildren(parentId, pageIndex, pageSize, out totalRecords, filter, ordering);
#endif
    }
}
