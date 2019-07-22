using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Ekom.Models
{
    public class Image
    {
        private IPublishedContent node;

        public Image(IPublishedContent node)
        {
            this.node = node;
        }

        public int Id {
            get
            {
                return node.Id;
            }
        }
        public Guid Key {
            get
            {
                return node.Key;
            }
        }
        public string Url {
            get
            {
                return node.Url();
            }
        }
        public string Name {
            get
            {
                return node.Name;
            }
        }
        public string Description {
            get
            {
                return node.HasProperty("description") && node.HasValue("description") ? node.Value<string>("description") : "";
            }
        }
    }
}
