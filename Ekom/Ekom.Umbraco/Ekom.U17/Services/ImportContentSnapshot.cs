using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Ekom.Umb.Services;

// Tracks only content already loaded by this import. It never discovers additional descendants.
internal sealed class ImportContentSnapshot
{
    private readonly List<(List<IContent> Items, Dictionary<int, int> Positions)> _lists = new();
    private readonly Dictionary<int, (int ParentId, int Position)> _childPositions = new();
    private readonly Dictionary<int, HashSet<int>> _unloadedChildren = new();
    private readonly HashSet<int> _deleted = new();
    private readonly HashSet<int> _dirtyParents = new();

    internal Dictionary<int, List<IContent>> Children { get; }

    internal ImportContentSnapshot(List<IContent> nodes, Dictionary<int, List<IContent>>? children = null)
    {
        Children = children ?? nodes.GroupBy(x => x.ParentId).ToDictionary(x => x.Key, x => x.ToList());
        Register(nodes);
        foreach (var entry in Children)
        {
            for (var i = 0; i < entry.Value.Count; i++)
                _childPositions[entry.Value[i].Id] = (entry.Key, i);
        }

        // Disabled nodes are intentionally absent from the loaded lists. Their loaded children
        // still belong to a moved/deleted subtree; bridge those gaps using the existing paths,
        // without loading the disabled nodes or changing import eligibility.
        var loadedIds = nodes.Select(x => x.Id).ToHashSet();
        foreach (var content in nodes)
        {
            if (loadedIds.Contains(content.ParentId) || string.IsNullOrEmpty(content.Path))
                continue;
            int? previousId = null;
            foreach (var segment in content.Path.Split(','))
            {
                if (!int.TryParse(segment, out var id))
                    continue;
                if (previousId.HasValue && !loadedIds.Contains(id))
                {
                    if (!_unloadedChildren.TryGetValue(previousId.Value, out var childrenThroughGap))
                        _unloadedChildren[previousId.Value] = childrenThroughGap = new HashSet<int>();
                    childrenThroughGap.Add(id);
                }
                previousId = id;
            }
        }
    }

    internal void Register(List<IContent> items)
    {
        if (_lists.Any(x => ReferenceEquals(x.Items, items)))
            return;

        var positions = new Dictionary<int, int>();
        for (var i = 0; i < items.Count; i++)
            positions[items[i].Id] = i;
        _lists.Add((items, positions));
    }

    internal void Added(IContent content)
    {
        // Call after appending to the canonical lists and parent bucket.
        // Umbraco assigns the ID on save; multiple unsaved nodes must not share an ID slot.
        if (content.Id == 0)
            return;

        foreach (var list in _lists)
        {
            if (list.Items.Count > 0 && ReferenceEquals(list.Items[^1], content))
                list.Positions[content.Id] = list.Items.Count - 1;
        }
        if (Children.TryGetValue(content.ParentId, out var children)
            && children.Count > 0 && ReferenceEquals(children[^1], content))
            _childPositions[content.Id] = (content.ParentId, children.Count - 1);
    }

    internal void Include(IContent content)
    {
        var canonical = _lists[0];
        if (!canonical.Positions.ContainsKey(content.Id)
            && (canonical.Items.Count == 0 || !ReferenceEquals(canonical.Items[^1], content)))
            canonical.Items.Add(content);
        if (!Children.TryGetValue(content.ParentId, out var children))
            Children[content.ParentId] = children = new List<IContent>();
        if (!_childPositions.ContainsKey(content.Id))
            children.Add(content);
        Added(content);
    }

    internal void Moved(IContent content, int oldParentId)
    {
        CompactChildren();
        if (_childPositions.TryGetValue(content.Id, out var old))
        {
            var children = Children[old.ParentId];
            children.RemoveAt(old.Position);
            for (var i = old.Position; i < children.Count; i++)
                _childPositions[children[i].Id] = (old.ParentId, i);
        }
        else if (Children.TryGetValue(oldParentId, out var oldChildren))
        {
            oldChildren.RemoveAll(x => x.Id == content.Id);
            for (var i = 0; i < oldChildren.Count; i++)
                _childPositions[oldChildren[i].Id] = (oldParentId, i);
        }

        if (!Children.TryGetValue(content.ParentId, out var newChildren))
            Children[content.ParentId] = newChildren = new List<IContent>();
        _childPositions[content.Id] = (content.ParentId, newChildren.Count);
        newChildren.Add(content);
    }

    private HashSet<int> Descendants(int rootId)
    {
        var ids = new HashSet<int>();
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(rootId);
        while (pending.TryPop(out var parentId))
        {
            if (!visited.Add(parentId))
                continue;
            if (_unloadedChildren.TryGetValue(parentId, out var childrenThroughGap))
            {
                foreach (var id in childrenThroughGap)
                    pending.Push(id);
            }
            if (!Children.TryGetValue(parentId, out var children))
                continue;
            foreach (var child in children)
            {
                if (child.Id != rootId && !_deleted.Contains(child.Id) && ids.Add(child.Id))
                    pending.Push(child.Id);
            }
        }
        return ids;
    }

    internal void RefreshDescendants(IContentService service, int rootId)
    {
        foreach (var batch in Descendants(rootId).Chunk(500))
        {
            var refreshed = service.GetByIds(batch).ToDictionary(x => x.Id);
            // Missing content is unsafe too: do not silently reuse a stale snapshot.
            if (batch.Any(id => !refreshed.ContainsKey(id)))
                throw new InvalidOperationException($"Could not refresh all loaded descendants of content {rootId} after moving it.");

            foreach (var content in refreshed.Values)
            {
                if (content.Trashed || (content.HasProperty("ekmDisableSync") && content.GetValue<bool>("ekmDisableSync")))
                    throw new InvalidOperationException($"Content {content.Id} is no longer eligible while refreshing descendants of {rootId}.");
                foreach (var list in _lists)
                {
                    if (list.Positions.TryGetValue(content.Id, out var position))
                        list.Items[position] = content;
                }
                if (_childPositions.TryGetValue(content.Id, out var child))
                {
                    if (child.ParentId != content.ParentId)
                        throw new InvalidOperationException($"Parent changed unexpectedly while refreshing content {content.Id}.");
                    Children[child.ParentId][child.Position] = content;
                }
            }
        }
    }

    internal bool IsDeleted(int id) => _deleted.Contains(id);

    internal void DeletedSubtree(int rootId)
    {
        var ids = Descendants(rootId);
        ids.Add(rootId);
        _deleted.UnionWith(ids);
        foreach (var id in ids)
        {
            if (_childPositions.Remove(id, out var child))
                _dirtyParents.Add(child.ParentId);
            Children.Remove(id);
        }
    }

    internal void CompactChildren()
    {
        foreach (var parentId in _dirtyParents)
        {
            if (!Children.TryGetValue(parentId, out var children))
                continue;
            children.RemoveAll(x => _deleted.Contains(x.Id));
            for (var i = 0; i < children.Count; i++)
                _childPositions[children[i].Id] = (parentId, i);
        }
        _dirtyParents.Clear();
    }

    internal void CompactDeleted()
    {
        CompactChildren();
        if (_deleted.Count == 0)
            return;
        foreach (var list in _lists)
        {
            list.Items.RemoveAll(x => _deleted.Contains(x.Id));
            list.Positions.Clear();
            for (var i = 0; i < list.Items.Count; i++)
                list.Positions[list.Items[i].Id] = i;
        }
        _deleted.Clear();
    }
}
