using Ekom.Utilities;

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

        public int Id => node.Id;
        public Guid Key => node.Key;

        public string Url => node.Url;

        public string Name => node.Name;

        public string Description => node.Properties.HasPropertyValue("description", storeAlias) ? node.GetValue("description", storeAlias) : "";
    }
}
