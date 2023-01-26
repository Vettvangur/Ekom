using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Ekom.Utilities;
using Ekom.Services;

namespace Ekom.Site.Controllers
{
    public class SiteController : UmbracoAuthorizedApiController
    {
        private readonly IContentService _cs;
        private readonly IMetafieldService _ms;
        public SiteController(IContentService cs, IMetafieldService ms)
        {
            _cs = cs;
            _ms = ms;
        }
        public object GetValue(string alias)
        {
            var node = _cs.GetById(1166);

            return node.GetMetafieldValue(alias);
        }

        public string UpdateMetafield()
        {
            var node = _cs.GetById(1166);

            node.SetOrUpdateMetafield("size", value: "99");
            
            if (node.Published)
            {
                _cs.SaveAndPublish(node);
            } else
            {
                _cs.Save(node);
            }

            return "Success";
        }

        public string UpdateMetafields()
        {
            var metaFields = _ms.GetMetafields();

            var metaField = metaFields.FirstOrDefault(x => x.Alias == "productFit");

            var values = metaField.Values.Take(2);

            var node = _cs.GetById(1207);
            
            node.SetOrUpdateMetafield("productFit", values: values.ToList());

            if (node.Published)
            {
                _cs.SaveAndPublish(node);
            }
            else
            {
                _cs.Save(node);
            }

            return "Success";
        }

    }
}
