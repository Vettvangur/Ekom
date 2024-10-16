using Ekom.Models.Comparers;
using Ekom.Utilities;
using Newtonsoft.Json;

namespace Ekom.Models;

public class Metafield
{
    public Metafield(UmbracoContent x)
    {
        var titleValues = JsonConvert.DeserializeObject<PropertyValue>(x.GetValue("title"));
        var values = x.GetValue("values");

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
        AllConditionsMustMatch = x.GetValue("allConditionsMustMatch").ConvertToBool();

        if (!string.IsNullOrEmpty(values))
        {
            var _values = JsonConvert.DeserializeObject<List<MetafieldValues>>(values);

            var orderedValues = _values
                .OrderBy(x => x.Values.Values.FirstOrDefault(), new SemiNumericComparer()).ToList();

            Values = orderedValues;
        }
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
    public bool AllConditionsMustMatch { get; set; }
    public List<MetafieldValues> Values { get; set; } = new List<MetafieldValues>();
}

public class MetafieldValues
{
    public string Id { get; set; }

    public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
}

public class MetafieldGrouped
{
    public Metafield Field { get; set; }
    public List<Dictionary<string, string>> Values { get; set; } = new List<Dictionary<string, string>>();
}
