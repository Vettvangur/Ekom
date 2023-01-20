using Ekom.Utilities;
using System;
using System.Collections.Generic;

namespace Ekom.Models
{
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
                _properties.Add(prop.Key, prop.Value);
            }

            Id = Convert.ToInt32(Properties.GetPropertyValue("id"));
            Key = new Guid(Properties.GetPropertyValue("__Key"));
            // Add culture parameter to get the correct nodename based on language ?
            Name = Properties.GetPropertyValue("nodeName");
            Path = Properties.GetPropertyValue("__Path");
            ContentTypeAlias = Properties.GetPropertyValue("ContentTypeAlias");
            Url = Properties.GetPropertyValue("url");
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
        public string Name { get; set; }
        public string Path { get; set; }
        public string ContentTypeAlias { get; set; }
        public bool IsDocumentType(string alias)
        {
            return ContentTypeAlias == alias;
        }

        public bool IsPublished()
        {        
            // TODO
            return true;
        }

        public string GetValue(string propertyAlias, string key = null)
        {
            return Properties.GetPropertyValue(propertyAlias, key);
        }
    }
}
