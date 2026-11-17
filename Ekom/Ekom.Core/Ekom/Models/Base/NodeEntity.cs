using Ekom.Exceptions;
using Ekom.Services;
using Ekom.Utilities;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Ekom.Models;

/// <summary>
/// Base Umbraco node entity
/// </summary>
public abstract class NodeEntity : INodeEntity
{
    /// <summary>
    /// Node Title
    /// </summary>
    public virtual string Title => Properties["nodeName"];

    /// <summary>
    /// Node Id
    /// </summary>
    public int Id { get; set; }


    /// <summary>
    /// Node Parent Id
    /// </summary>
    public virtual int ParentId
    {
        get
        {
            if (int.TryParse(GetValue("parentID"), out int _parentId))
            {
                return _parentId;
            }

            return 0;
        }
    }

    /// <summary>
    /// Node Parent Guid Key
    /// </summary>
    public virtual Guid ParentKey
    {
        get
        {
            if (Guid.TryParse(GetValue("parentKey"), out Guid _parentKey))
            {
                return _parentKey;
            }

            return Guid.Empty;
        }
    }

    /// <summary>
    /// Node Guid Key
    /// </summary>
    public virtual Guid Key
        => GetParsed(ref _key, Properties["__Key"], static key =>
        {
            if (!Guid.TryParse(key, out Guid _key))
            {
                throw new NodeEntityException("No key present for node.");
            }

            return _key;
        });

    /// <summary>
    /// 
    /// </summary>
    public virtual int SortOrder
        => GetParsed(ref _sortOrder, Properties["sortOrder"], static value => int.TryParse(value, out int _value) ? _value : 0);

    /// <summary>
    /// Level of node in Umbraco content tree hierarchy
    /// </summary>
    public virtual int Level
        => GetParsed(ref _level, Properties["level"], static value => int.TryParse(value, out int _value) ? _value : 0);

    /// <summary>
    ///
    /// </summary>
    public virtual bool VariesByCulture
    {
        get
        {
            if (string.IsNullOrEmpty(Properties.GetPropertyValue("__VariesByCulture")))
            {
                return false;
            }

            return Properties.GetPropertyValue("__VariesByCulture").IsBoolean();
        }
    }

    /// <summary>
    /// CSV of node id's describing hierarchy from left to right leading up to node.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public string Path { get; set; }

    /// <summary>
    /// Array of node id's describing hierarchy from left to right leading up to node.
    /// </summary>
    public string[] PathArray { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime CreateDate
    {
        get
        {
            return GetParsed(ref _createDate, Properties["createDate"], UtilityService.ConvertToDatetime);
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public virtual DateTime UpdateDate
    {
        get
        {
            return GetParsed(ref _updateDate, Properties["updateDate"], UtilityService.ConvertToDatetime);
        }
    }
    /// <summary>
    /// Possibly unneeded
    /// </summary>
    public virtual string ContentTypeAlias => Properties["__NodeTypeAlias"];

    /// <summary>
    /// Read only dictionary of all umbraco base and custom properties for this item
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IReadOnlyDictionary<string, string> Properties
    {
        get
        {
            Dictionary<string, string> properties = _properties;
            PropertiesView? view = _propertiesView;

            // Derived types may swap the dictionary, only then is a new wrapper needed.
            if (view == null || !ReferenceEquals(view.Source, properties))
            {
                view = new PropertiesView(properties, new ReadOnlyDictionary<string, string>(properties));
                _propertiesView = view;
            }

            return view.ReadOnly;
        }
    }

    private sealed record PropertiesView(Dictionary<string, string> Source, ReadOnlyDictionary<string, string> ReadOnly);

    private PropertiesView? _propertiesView;

    private sealed record Parsed<T>(string Raw, T Value);

    private Parsed<Guid>? _key;
    private Parsed<int>? _sortOrder;
    private Parsed<int>? _level;
    private Parsed<DateTime>? _createDate;
    private Parsed<DateTime>? _updateDate;

    /// <summary>
    /// Parses a property value once and reuses the result until the stored value changes.
    /// Listings sort by these values, so they are read once per comparison.
    /// </summary>
    private static T GetParsed<T>(ref Parsed<T>? slot, string raw, Func<string, T> parse)
    {
        Parsed<T>? parsed = slot;
        if (parsed != null && string.Equals(parsed.Raw, raw, StringComparison.Ordinal))
        {
            return parsed.Value;
        }

        T value = parse(raw);
        slot = new Parsed<T>(raw, value);

        return value;
    }

    /// <summary>
    /// All node properties
    /// </summary>
    protected Dictionary<string, string> _properties = new Dictionary<string, string>();

    /// <summary>
    /// ctor
    /// </summary>
    protected NodeEntity() { }

    /// <summary>
    /// Construct Node
    /// </summary>
    /// <param name="content"></param>
    protected NodeEntity(UmbracoContent content)
    {
        _properties = new Dictionary<string, string>(content.Properties);

        Id = (int.TryParse(GetValue("id"), out int _value)) ? _value : 0;
        Path = content.Path;
        PathArray = !string.IsNullOrEmpty(content.Path) ? content.Path.Split(',') : new string[] { };


    }

    /// <summary>
    /// Get value in properties by store or languge
    /// </summary>
    /// <param name="propAlias"></param>
    /// <param name="alias"></param>
    /// <param name="fallback"></param>
    public string GetValue(string propAlias, string? alias = null, bool fallback = false)
    {
        return Properties.GetPropertyValue(propAlias, alias, fallback);
    }

    public string GetRawValue(string propAlias)
    {
        return Properties.GetRawPropertyValue(propAlias);
    }
}
