using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core.Models;

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
        public virtual int Id => Convert.ToInt32(Properties.GetPropertyValue("id"));

        /// <summary>
        /// 
        /// </summary>
        public virtual Guid Key
        {
            get
            {
                var key = Properties.GetPropertyValue("key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual int SortOrder => Convert.ToInt32(Properties.GetPropertyValue("sortOrder"));
        /// <summary>
        /// Level of node in Umbraco content tree hierarchy
        /// </summary>
        public virtual int Level => Convert.ToInt32(Properties.GetPropertyValue("level"));

        /// <summary>
        /// CSV of node id's describing hierarchy from left to right leading up to node.
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual string Path => Properties.GetPropertyValue("path");
        /// <summary>
        /// 
        /// </summary>
        public virtual DateTime CreateDate
        {
            get
            {
                return ExamineService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public virtual DateTime UpdateDate
        {
            get
            {
                return ExamineService.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
            }
        }
        /// <summary>
        /// Possibly unneeded
        /// </summary>
        public virtual string ContentTypeAlias => Properties.GetPropertyValue("nodeTypeAlias");

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
        public NodeEntity() { }

        /// <summary>
        /// Construct Node from Examine item
        /// </summary>
        /// <param name="item"></param>
        public NodeEntity(ISearchResult item)
        {
            foreach (var field in item.Fields.Where(x => !x.Key.StartsWith("__")))
            {
                _properties.Add(field.Key, field.Value);
            }
        }

        /// <summary>
        /// Construct Node from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        public NodeEntity(IContent node)
        {
            _properties = CreateDefaultUmbracoProperties(node);

            foreach (var prop in node.Properties)
            {
                _properties.Add(prop.Alias, prop.Value?.ToString());
            }
        }

        Dictionary<string, string> CreateDefaultUmbracoProperties(IContent node)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "id",
                    node.Id.ToString()
                },
                {
                    "nodeName",
                    node.Name
                },
                {
                    "key",
                    node.Key.ToString()
                },
                {
                    "path",
                    node.Path
                },
                {
                    "level",
                    node.Level.ToString()
                },
                {
                    "sortOrder",
                    node.SortOrder.ToString()
                },
                {
                    "parentID",
                    node.ParentId.ToString()
                },
                {
                    "writerID",
                    node.WriterId.ToString()
                },
                {
                    "creatorID",
                    node.CreatorId.ToString()
                },
                {
                    "nodeTypeAlias",
                    node.ContentType.Alias
                },
                {
                    "updateDate",
                    node.UpdateDate.ToString("yyyyMMddHHmmssfff")
                },
                {
                    "createDate",
                    node.CreateDate.ToString("yyyyMMddHHmmssfff")
                }
            };

            return properties;
        }

        /// <summary>
        /// Get value in properties
        /// </summary>
        /// <param name="alias"></param>
        public virtual string GetPropertyValue(string alias)
        {
            return Properties.GetPropertyValue(alias);
        }
    }
}
