using Ekom.Utilities;
using System;

namespace Ekom.Models
{
    public class Image
    {
        private readonly UmbracoContent node;
        private readonly string storeAlias;
        public Image(UmbracoContent node, string storeAlias)
        {
            this.node = node;
            this.storeAlias = storeAlias;
        }

        public int Id
        {
            get
            {
                return node.Id;
            }
        }
        public Guid Key
        {
            get
            {
                return node.Key;
            }
        }
        public string Url
        {
            get
            {
                return node.Url;
            }
        }
        public string Name
        {
            get
            {
                return node.Name;
            }
        }
        public string Description
        {
            get
            {
                return node.Properties.HasPropertyValue("description", storeAlias) ? node.Properties.GetPropertyValue("description", storeAlias) : "";
            }
        }
    }
}
