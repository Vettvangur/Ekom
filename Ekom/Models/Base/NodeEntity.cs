using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public virtual int Id => Convert.ToInt32(Properties.GetPropertyValue("__NodeId"));


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
        public virtual string Path => Properties.GetPropertyValue("__Path");
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
        public NodeEntity() { }

        /// <summary>
        /// Construct Node from Examine item
        /// </summary>
        /// <param name="item"></param>
        public NodeEntity(ISearchResult item)
        {
            foreach (var field in item.Values)
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
                _properties.Add(prop.Alias, prop.GetValue()?.ToString());
            }
        }

        Dictionary<string, string> CreateDefaultUmbracoProperties(IContent node)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "__NodeId",
                    node.Id.ToString()
                },
                {
                    "nodeName",
                    node.Name
                },
                {
                    "__Key",
                    node.Key.ToString()
                },
                {
                    "__Path",
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
                    "__NodeTypeAlias",
                    node.ContentType.Alias
                },
                {
                    "updateDate",
                    node.UpdateDate.ToString("yyyyMMddHHmmssfff")
                },
                {
                    "createDate",
                    node.CreateDate.ToString("yyyyMMddHHmmssfff")
                },
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
