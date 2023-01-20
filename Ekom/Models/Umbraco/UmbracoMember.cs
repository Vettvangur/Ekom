using Ekom.Utilities;
using System;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class UmbracoMember
    {
        public UmbracoMember()
        {

        }
        public UmbracoMember(IDictionary<string, string> defaultProperties, Dictionary<string, string> contentProperies)
        {
            _properties = new Dictionary<string, string>(defaultProperties);

            foreach (var prop in contentProperies)
            {
                _properties.Add(prop.Key, prop.Value);
            }

            Id = Convert.ToInt32(Properties.GetPropertyValue("id"));
            Key = new Guid(Properties.GetPropertyValue("__Key"));
            Name = Properties.GetPropertyValue("nodeName");
            UserName = Properties.GetPropertyValue("loginName");
            Email = Properties.GetPropertyValue("email");
        }

        readonly Dictionary<string, string> _properties;
        /// <summary>
        /// All node properties
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties => _properties;

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
    }
}
