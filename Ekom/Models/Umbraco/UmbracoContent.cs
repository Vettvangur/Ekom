using Ekom.Utilities;

namespace Ekom.Models;

public class UmbracoContent
{
    public UmbracoContent()
    {

    }

    public UmbracoContent(
        IDictionary<string, string> defaultProperties,
        Dictionary<string, string> contentProperies)
    {
        _properties = new Dictionary<string, string>(defaultProperties);

        foreach (var prop in contentProperies)
        {
            _properties[prop.Key] = prop.Value;
        }

        if (int.TryParse(GetValue("id"), out var id)) Id = id;
        if (Guid.TryParse(GetValue("__Key"), out var key)) Key = key;
        Name = GetValue("nodeName");
        Path = GetValue("__Path");
        if (int.TryParse(GetValue("level"), out var level)) Level = level;
        ContentTypeAlias = GetValue("__NodeTypeAlias");
        Url = GetValue("url");
        if (Guid.TryParse(GetValue("parentKey"), out var parentKey)) ParentKey = parentKey;
    }

    readonly Dictionary<string, string> _properties;
    /// <summary>
    /// All node properties
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties => _properties;

    public int Id { get; set; }
    public Guid Key { get; set; }
    // Used for Media Url. Think we should remove this for simplicity and get the url specificly. Used in the Image class
    public string Url { get; set; }
    public Guid ParentKey { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public int Level { get; set; }
    public string ContentTypeAlias { get; set; }

    public bool IsDocumentType(string alias) => ContentTypeAlias == alias;

    public string GetValue(string propertyAlias, string key = null) =>
                Properties.GetPropertyValue(propertyAlias, key);
}
