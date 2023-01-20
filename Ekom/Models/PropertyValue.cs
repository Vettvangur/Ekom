using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class PropertyValue
    {
        [JsonProperty("dtdGuid")]
        public Guid DtdGuid { get; set; }
        [JsonProperty("values")]
        public IDictionary<string, object> Values = new Dictionary<string, object>();
        [JsonProperty("type")]
        public string Type {get; set;}
    }
}
