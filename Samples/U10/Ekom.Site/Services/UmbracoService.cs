using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Ekom.Site;
public class UmbracoService
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;

    public UmbracoService(
        ILogger<UmbracoService> logger,
        IUmbracoContextFactory umbracoContextFactory
    )
    {
        _umbracoContextFactory = umbracoContextFactory;
    }

    public IEnumerable<Umbraco.Cms.Core.Routing.Domain> GetDomains()
    {
        using (var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            return umbracoContextReference.UmbracoContext.Domains.GetAll(true);
        }
    }

    internal IPublishedContent GetNode(string key, string culture = null)
    {
        IPublishedContent node = null;

        using (var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            if (Guid.TryParse(key, out Guid _key))
            {
                node = umbracoContextReference.UmbracoContext.Content.GetById(false, _key);
            }
            else
            {
                node = umbracoContextReference.UmbracoContext.Content.GetByRoute(false, key, null, culture);
            }
        }

        return node;
    }

    internal IPublishedContent GetNodeById(int id)
    {
        IPublishedContent node = null;

        using (var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            node = umbracoContextReference.UmbracoContext.Content.GetById(false, id);
        }

        return node;
    }

    internal IPublishedContent GetMedia(string mediaId)
    {
        if (mediaId.InvariantContains(","))
        {
            mediaId = mediaId.Split(',')[0];
        }

        IPublishedContent node = null;

        if (UdiParser.TryParse(mediaId, out Udi _udiId))
        {
            using (var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
            {
                node = umbracoContextReference.UmbracoContext.Media.GetById(false, _udiId);
            }

            return node;
        }
        
        return null;

    }
    
    public bool IsInCurrentPath(IPublishedContent model, IPublishedContent node)
    {
        string[] path = (!string.IsNullOrEmpty(model.Path)) ? model.Path.Split(',') : new string[1];
        int isInCurrentPath = Array.IndexOf(path, node.Id.ToString());

        if (isInCurrentPath >= 0)
        {
            return true;
        }

        return false;
    }
}
