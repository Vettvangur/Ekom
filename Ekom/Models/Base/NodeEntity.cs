using Ekom.Exceptions;
using Ekom.Services;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        /// 
        /// </summary>
        public virtual string Title => Properties["nodeName"];

        /// <summary>
        /// 
        /// </summary>
        public virtual int Id
        {
            get
            {
                if (int.TryParse(Properties.GetPropertyValue("id"), out int _value))
                {
                    return _value;
                }

                return 0;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public virtual int ParentId
        {
            get
            {
                if (int.TryParse(Properties.GetPropertyValue("parentID"), out int _parentId))
                {
                    return _parentId;
                }

                return 0;
            }
        }

        /// <summary>
        /// 
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
        [JsonIgnore]
        [XmlIgnore]
        public virtual string Path => Properties.GetPropertyValue("__Path");
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
        /// Construct Node from Examine item
        /// </summary>
        /// <param name="item"></param>
        protected NodeEntity(UmbracoContent content)
        {
            _properties = content.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
