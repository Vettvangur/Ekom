using Ekom.Models.Comparers;
using Ekom.Utilities;
using Newtonsoft.Json;
using System.Xml.Serialization;

namespace Ekom.Models
{
    public class Metafield : MetafieldBase
    {
        public Metafield(UmbracoContent x) : base(x)
        {
            string values = x.GetValue("values");
            Values = OrderedValues(values);
        }
        [JsonIgnore]
        [XmlIgnore]
        public List<MetafieldValues> Values { get; set; } = new List<MetafieldValues>();
    }

    public class MetafieldInternal : MetafieldBase
    {
        public MetafieldInternal(UmbracoContent x) : base(x)
        {
            var values = x.GetValue("values");
            Values = OrderedValues(values);
        }
        
        public List<MetafieldValues> Values { get; set; } = new List<MetafieldValues>();
    }

    public class MetafieldBase
    {
        public MetafieldBase(UmbracoContent x)
        {
            var titleValues = JsonConvert.DeserializeObject<PropertyValue>(x.GetValue("title"));
            

            Id = x.Id;
            Key = x.Key;
            Title = titleValues.Values.ToDictionary(z => z.Key, z => z.Value.ToString());
            Alias = string.IsNullOrEmpty(x.GetValue("alias")) ? x.Name.ToCamelCase() : x.GetValue("alias");
            Description = x.GetValue("description");
            Name = x.Name;
            Required = x.GetValue("required").ConvertToBool();
            Filterable = x.GetValue("filterable").ConvertToBool();
            EnableMultipleChoice = x.GetValue("enableMultipleChoice").ConvertToBool();
            ReadOnly = x.GetValue("readOnly").ConvertToBool();

        
            

        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public Dictionary<string, string> Title { get; set; } = new Dictionary<string, string>();
        public string Description { get; set; }
        public bool Filterable { get; set; }
        public bool EnableMultipleChoice { get; set; }
        public bool Required { get; set; }
        public bool ReadOnly { get; set; }

        public List<MetafieldValues> OrderedValues(string values)
        {
            if (!string.IsNullOrEmpty(values))
            {
                var _values = JsonConvert.DeserializeObject<List<MetafieldValues>>(values);

                var orderedValues = _values
                    //.Where(x => !x.Values.ContainsKey("undefined")).ToList()
                    .OrderBy(x => x.Values.Values.FirstOrDefault(), new SemiNumericComparer()).ToList();

                return orderedValues;
            }

            return new List<MetafieldValues>();
        }

    }

    public class MetafieldValues
    {
        public string Id { get; set; }

        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }

    public class MetafieldGrouped
    {
        public Metafield Field { get; set; }
        public List<Dictionary<string,string>> Values { get; set; } = new List<Dictionary<string, string>>();
    }
}
