using Ekom.Utilities;

namespace Ekom.Models;

public class UmbracoContent
{
    public UmbracoContent()
    {

    }

    public UmbracoContent(
          IDictionary<string, string> defaultProperties,
          Dictionary<string, string> contentProperties)
    {
        _properties = new Dictionary<string, string>(
             defaultProperties.Concat(contentProperties)
                 .ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? string.Empty));



        // Parse properties safely
        if (_properties.TryGetValue("id", out string? idValue) && int.TryParse(idValue, out int id))
            Id = id;

        if (_properties.TryGetValue("__Key", out string? keyValue) && Guid.TryParse(keyValue, out Guid key))
            Key = key;

        if (_properties.TryGetValue("parentKey", out string? parentKeyValue) && Guid.TryParse(parentKeyValue, out Guid parentKey))
            ParentKey = parentKey;

        if (_properties.TryGetValue("level", out string? levelValue) && int.TryParse(levelValue, out int level))
            Level = level;

        // Get values safely with default fallbacks
        Name = _properties.GetValueOrDefault("nodeName", string.Empty);
        Path = _properties.GetValueOrDefault("__Path", string.Empty);
        ContentTypeAlias = _properties.GetValueOrDefault("__NodeTypeAlias", string.Empty);
        Url = _properties.GetValueOrDefault("url", string.Empty);
    }

    private readonly Dictionary<string, string> _properties;

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

    public string GetValue(string propertyAlias, string? key = null) =>
                Properties.GetPropertyValue(propertyAlias, key);
    public string GetRawValue(string propertyAlias) =>
                Properties.GetRawPropertyValue(propertyAlias);

}
