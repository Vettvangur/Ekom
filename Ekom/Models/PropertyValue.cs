using Ekom.Utilities;
using Newtonsoft.Json;

namespace Ekom.Models
{
    public class PropertyValue
    {
        [JsonProperty("dtdGuid")]
        public Guid DtdGuid { get; set; }
        [JsonProperty("values")]
        public IDictionary<string, object> Values = new Dictionary<string, object>();
        [JsonProperty("type")]
        public PropertyEditorType Type {get; set;}
    }
}
