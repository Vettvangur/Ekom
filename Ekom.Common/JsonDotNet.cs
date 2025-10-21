using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Ekom;

public static class EkomJsonDotNet
{
    private static readonly Dictionary<string, Type> _fullNameMap = new(); // "Type, Assembly"
    private static readonly Dictionary<string, Type> _nameOnlyMap = new(); // "Namespace.Type"

    private static readonly RemappingBinder Binder = new(
        fullNameMap: _fullNameMap,
        nameOnlyMap: _nameOnlyMap,
        nsRewrites: new[] {
            ("Ekom.Models.Behaviors.", "Ekom.Models."),
            ("Ekom.Models.OrderedObjects.", "Ekom.Models."),
        },
        log: (m, _) => {  }
    );

    public static void AddTypeMap(string oldTypeName, Type newType)
        => _fullNameMap[oldTypeName] = newType;

    public static void AddTypeMapByName(string oldFullName, Type newType)
        => _nameOnlyMap[oldFullName] = newType;

    public static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        SerializationBinder = Binder,
        MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead
    };

    public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);
}


public sealed class RemappingBinder : ISerializationBinder
{
    private readonly Dictionary<string, Type> _fullNameMap;   // "Type, Assembly" -> Type
    private readonly Dictionary<string, Type> _nameOnlyMap;   // "Namespace.Type" -> Type
    private readonly List<(string fromPrefix, string toPrefix)> _nsRewrites;
    private readonly Action<string, Exception?>? _log;         // optional

    public RemappingBinder(
        Dictionary<string, Type> fullNameMap,
        Dictionary<string, Type>? nameOnlyMap = null,
        IEnumerable<(string fromPrefix, string toPrefix)>? nsRewrites = null,
        Action<string, Exception?>? log = null)
    {
        _fullNameMap = fullNameMap;
        _nameOnlyMap = nameOnlyMap ?? new();
        _nsRewrites = nsRewrites?.ToList() ?? new();
        _log = log;
    }

    public Type BindToType(string? assemblyName, string typeName)
    {
        // 1) Exact map on "Type, Assembly"
        var key = string.IsNullOrEmpty(assemblyName) ? typeName : $"{typeName}, {assemblyName}";
        if (_fullNameMap.TryGetValue(key, out var mapped)) return mapped;

        // 2) Map by type full name only
        if (_nameOnlyMap.TryGetValue(typeName, out var byName)) return byName;

        // 3) Namespace rewrite heuristics (e.g., Behaviors. -> (nothing))
        foreach (var (from, to) in _nsRewrites)
        {
            if (typeName.StartsWith(from, StringComparison.Ordinal))
            {
                var rewritten = to + typeName.Substring(from.Length);
                // try full-name map with assembly
                var rewrittenKey = string.IsNullOrEmpty(assemblyName) ? rewritten : $"{rewritten}, {assemblyName}";
                if (_fullNameMap.TryGetValue(rewrittenKey, out var r1)) return r1;
                if (_nameOnlyMap.TryGetValue(rewritten, out var r2)) return r2;

                // scan loaded assemblies for rewritten name
                var tRewritten = ResolveFromLoadedAssemblies(rewritten);
                if (tRewritten != null) return tRewritten;
            }
        }

        // 4) Scan loaded assemblies for the original name (no throw)
        var t = ResolveFromLoadedAssemblies(typeName);
        if (t != null) return t;

        // 5) Final fallback: helpful error (or return typeof(object) to swallow unknown types)
        var msg = $"Json type resolution failed for '{key}'. " +
                  "Register a mapping via EkomJsonDotNet.AddTypeMap(...) or add a namespace rewrite.";
        _log?.Invoke(msg, null);


        throw new JsonSerializationException(msg);
    }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }

    private static Type? ResolveFromLoadedAssemblies(string fullTypeName)
    {
        // Try Type.GetType first (no throw)
        var t = Type.GetType(fullTypeName, throwOnError: false);
        if (t != null) return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                t = asm.GetType(fullTypeName, throwOnError: false, ignoreCase: false);
                if (t != null) return t;
            }
            catch { /* skip dynamic/reflection-only assemblies */ }
        }
        return null;
    }
}

