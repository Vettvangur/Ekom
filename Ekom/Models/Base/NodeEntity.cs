using Ekom.Exceptions;
using Ekom.Services;
using Ekom.Utilities;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Ekom.Models
{
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
        {
            get
            {
                var key = Properties.GetPropertyValue("__Key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new NodeEntityException("No key present for node.");
                }

                return _key;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual int SortOrder
        {

            get
            {
                if (int.TryParse(Properties.GetPropertyValue("sortOrder"), out int _value))
                {
                    return _value;
                }

                return 0;
            }

        }
        /// <summary>
        /// Level of node in Umbraco content tree hierarchy
        /// </summary>
        public virtual int Level
        {
            get
            {
                if (int.TryParse(Properties.GetPropertyValue("level"), out int _value))
                {
                    return _value;
                }

                return 0;
            }
        }

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
        public string Path { get; set;  }
        
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
                return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public virtual DateTime UpdateDate
        {
            get
            {
                return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
            }
        }
        /// <summary>
        /// Possibly unneeded
        /// </summary>
        public virtual string ContentTypeAlias => Properties.GetPropertyValue("__NodeTypeAlias");

        /// <summary>
        /// Read only dictionary of all umbraco base and custom properties for this item
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties => new ReadOnlyDictionary<string, string>(_properties);

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
        /// <param name="item"></param>
        protected NodeEntity(UmbracoContent content)
        {
            _properties = content.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Id = (int.TryParse(GetValue("id"), out int _value)) ? _value : 0;
            Path = content.Path;
            PathArray = !string.IsNullOrEmpty(content.Path) ? content.Path.Split(',') : new string[] { };
        }

        /// <summary>
        /// Get value in properties by store or languge
        /// </summary>
        /// <param name="propAlias"></param>
        /// <param name="alias"></param>
        public string GetValue(string propAlias, string alias = null)
        {
            return Properties.GetPropertyValue(propAlias, alias);
        }
    }
}
