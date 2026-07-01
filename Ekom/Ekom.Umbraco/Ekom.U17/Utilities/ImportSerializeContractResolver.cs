using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace Ekom.Utilities;

internal sealed class ImportSerializeContractResolver : DefaultContractResolver
{
    private readonly HashSet<string> _ignoredPropertyNames = new();

    public void IgnorePropertyByName(string propertyName)
    {
        _ignoredPropertyNames.Add(propertyName);
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.PropertyName != null && _ignoredPropertyNames.Contains(property.PropertyName))
        {
            property.ShouldSerialize = _ => false;
        }

        return property;
    }
}
