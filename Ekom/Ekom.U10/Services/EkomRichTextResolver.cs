using Umbraco.Cms.Core.Templates;
using Umbraco.Cms.Core.Web;

namespace Ekom.Umb.Services;

interface IEkomRichTextResolver
{
    string ResolveLocalLinks(string value);
}

internal sealed class EkomRichTextResolver : IEkomRichTextResolver
{
    private readonly IUmbracoContextFactory _contextFactory;
    private readonly HtmlLocalLinkParser _linkParser;

    public EkomRichTextResolver(
        IUmbracoContextFactory contextFactory,
        HtmlLocalLinkParser linkParser)
    {
        _contextFactory = contextFactory;
        _linkParser = linkParser;
    }

    public string ResolveLocalLinks(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("{localLink:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        using var cref = _contextFactory.EnsureUmbracoContext();

        return _linkParser.EnsureInternalLinks(value, preview: false);
    }
}
