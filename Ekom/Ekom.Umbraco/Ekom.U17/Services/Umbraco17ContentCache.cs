using Ekom.Models;
using System.Collections.Concurrent;

namespace Ekom.Umb.Services;

internal sealed class Umbraco17ContentCache
{
    private readonly ConcurrentDictionary<int, UmbracoContent> _contentById = new();

    public IEnumerable<UmbracoContent> Values => _contentById.Values;

    public void AddOrUpdate(UmbracoContent content) => _contentById[content.Id] = content;

    public bool TryGetById(int id, out UmbracoContent? content) => _contentById.TryGetValue(id, out content);
}
