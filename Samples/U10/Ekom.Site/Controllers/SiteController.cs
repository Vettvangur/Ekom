using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Ekom.Utilities;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;

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

            var metafieldValues = new Dictionary<string, List<Models.MetafieldValues>>
            {
                {
                    "size",
                    new List<Models.MetafieldValues>() {
                    new Models.MetafieldValues() {
                        Values = new Dictionary<string, string>() {
                            { "", "size test" }
                        }
                    }
                }
                },
                {
                    "hallo",
                    new List<Models.MetafieldValues>() {
                    new Models.MetafieldValues() {
                        Values = new Dictionary<string, string>() {
                            { "asd", "hallo test" }
                        }
                    }
                }
                }
            };

            node.SetMetafield(metafieldValues);

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

        //[HttpGet]
        //public List<string> UpdateMetafield2()
        //{
        //    var list = new List<string>();
        //    var node = _cs.GetById(1208);

        //    var unitValue = "pokar32";

        //    var unit = node.GetMetafieldValue("hallo");

        //    list.Add(string.Join(",", unit.SelectMany(x => x.Values)));

        //    if (unit != null && !unit.SelectMany(x => x.Values).Any(z => z == unitValue))
        //    {
        //        node.SetMetafield("hallo", value: unitValue);

        //        if (node.Published)
        //        {
        //            _cs.SaveAndPublish(node);
        //            list.Add("published");
        //        }
        //        else
        //        {
        //            _cs.Save(node);
        //            list.Add("saved");
        //        }

        //    }

        //    return list;
        //}

        //public string UpdateMetafields()
        //{
        //    var metaFields = _ms.GetMetafields();

        //    var metaField = metaFields.FirstOrDefault(x => x.Alias == "productFit");

        //    var values = metaField.Values.Take(2);

        //    var node = _cs.GetById(1207);

        //    node.SetMetafield("productFit", values: values.ToList());

        //    if (node.Published)
        //    {
        //        _cs.SaveAndPublish(node);
        //    }
        //    else
        //    {
        //        _cs.Save(node);
        //    }

        //    return "Success";
        //}

    }
}
