using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ekom;

public class PropertyValue
{
    [JsonProperty("dtdGuid")]
    public Guid DtdGuid { get; set; }

    [JsonProperty("values")]
    public IDictionary<string, object> Values = new Dictionary<string, object>();

    [JsonProperty("type")]
    [JsonConverter(typeof(StringEnumConverter))]
    public PropertyEditorType Type {get; set;}
}

/// <summary>
/// Ekom Property Editor Type
/// </summary>
public enum PropertyEditorType
{
    Empty,
    Store,
    Language,
}
