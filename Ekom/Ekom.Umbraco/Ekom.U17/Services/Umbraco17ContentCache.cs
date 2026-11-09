using Ekom.Models;
using System.Collections.Concurrent;

namespace Ekom.Umb.Services;

internal sealed class Umbraco17ContentCache
{
    private readonly Configuration _config;

    public Umbraco17ContentCache(Configuration config) => _config = config;

    private ConcurrentDictionary<int, UmbracoContent> _contentById =>
        _config.CacheRebuild.Get(this, nameof(_contentById), static () => new ConcurrentDictionary<int, UmbracoContent>());

    public IEnumerable<UmbracoContent> Values => _contentById.Values;

    public void AddOrUpdate(UmbracoContent content) => _contentById[content.Id] = content;

    public void Clear() => _contentById.Clear();

    public bool TryGetById(int id, out UmbracoContent? content) => _contentById.TryGetValue(id, out content);
}
