using Umbraco.Cms.Core.Models.PublishedContent;

namespace Ekom.Umb.Services;

internal sealed class EkomCacheBuildContext
{
    private readonly AsyncLocal<State?> _state = new();

    public IDisposable Begin(IEnumerable<IPublishedContent> nodes)
    {
        if (_state.Value != null)
        {
            throw new InvalidOperationException("An Ekom cache build is already in progress.");
        }

        var state = new State(nodes);
        _state.Value = state;
        return new Scope(this, state);
    }

    public bool TryGetNodes(string contentTypeAlias, out IReadOnlyList<IPublishedContent> nodes)
    {
        var state = _state.Value;
        if (state == null)
        {
            nodes = Array.Empty<IPublishedContent>();
            return false;
        }

        nodes = state.NodesByContentType.TryGetValue(contentTypeAlias, out var matchingNodes)
            ? matchingNodes
            : Array.Empty<IPublishedContent>();
        return true;
    }

    public bool TryGetNodeInfo(
        IPublishedContent node,
        out int? parentId,
        out Guid? parentKey,
        out string path)
    {
        var state = _state.Value;
        if (state == null || !state.NodeInfoByKey.TryGetValue(node.Key, out var nodeInfo))
        {
            parentId = null;
            parentKey = null;
            path = string.Empty;
            return false;
        }

        parentId = nodeInfo.ParentKey.HasValue && state.IdByKey.TryGetValue(nodeInfo.ParentKey.Value, out var id)
            ? id
            : null;
        parentKey = nodeInfo.ParentKey;
        path = GetPath(nodeInfo, state);
        return true;
    }

    private static string GetPath(NodeInfo nodeInfo, State state)
    {
        if (nodeInfo.Path != null)
        {
            return nodeInfo.Path;
        }

        var path = nodeInfo.ParentKey.HasValue && state.NodeInfoByKey.TryGetValue(nodeInfo.ParentKey.Value, out var parent)
            ? $"{GetPath(parent, state)},{nodeInfo.Id}"
            : $"-1,{nodeInfo.Id}";

        nodeInfo.Path = path;
        return path;
    }

    private void End(State state)
    {
        if (ReferenceEquals(_state.Value, state))
        {
            _state.Value = null;
        }
    }

    private sealed class Scope(EkomCacheBuildContext context, State state) : IDisposable
    {
        public void Dispose() => context.End(state);
    }

    private sealed class State
    {
        public State(IEnumerable<IPublishedContent> nodes)
        {
            var allNodes = nodes.DistinctBy(x => x.Key).ToList();
            IdByKey = allNodes.ToDictionary(x => x.Key, x => x.Id);
            NodeInfoByKey = allNodes.ToDictionary(x => x.Key, x => new NodeInfo(x.Id, x.Parent?.Key));
            NodesByContentType = allNodes
                .GroupBy(x => x.ContentType.Alias)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<IPublishedContent>)x.ToList());
        }

        public Dictionary<Guid, int> IdByKey { get; }

        public Dictionary<Guid, NodeInfo> NodeInfoByKey { get; }

        public Dictionary<string, IReadOnlyList<IPublishedContent>> NodesByContentType { get; }
    }

    private sealed class NodeInfo(int id, Guid? parentKey)
    {
        public int Id { get; } = id;

        public Guid? ParentKey { get; } = parentKey;

        public string? Path { get; set; }
    }
}
