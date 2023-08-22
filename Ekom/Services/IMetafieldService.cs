using Ekom.Models;
using Newtonsoft.Json.Linq;

namespace Ekom.Services
{
    public interface IMetafieldService
    {
        IEnumerable<Metafield> GetMetafields();
        List<Metavalue> SerializeMetafields(string jsonValue, int nodeId);
        IEnumerable<MetafieldGrouped> Filters(IEnumerable<IProduct> products, bool filterable = true);
        IEnumerable<IProduct> FilterProducts(IEnumerable<IProduct> products, ProductQuery query);
        JArray SetMetafield(string json, Dictionary<string, List<MetafieldValues>> values);
        List<Dictionary<string, string>> GetMetaFieldValue(string json, int nodeId, string metafieldAlias);
        string GetMetaFieldValue(IProduct product, string metafieldAlias, string culture = "");
    }
}
