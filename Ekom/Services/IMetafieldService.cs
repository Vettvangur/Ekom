using Ekom.Models;
using Newtonsoft.Json.Linq;

namespace Ekom.Services
{
    public interface IMetafieldService
    {
        IEnumerable<Metafield> GetMetafields();
        List<Metavalue> SerializeMetafields(string jsonValue);
        IEnumerable<MetafieldGrouped> Filters(IEnumerable<IProduct> products, bool filterable = true);
        IEnumerable<IProduct> FilterProducts(IEnumerable<IProduct> products, ProductQuery query);
        JArray AddOrUpdateMetaField(string json, string metaFieldAlias, List<MetafieldValues> values = null, string value = null);
        JArray AddOrUpdateMetaField(string json, Metafield field, List<MetafieldValues> values = null, string value = null);
        List<Dictionary<string, string>> GetMetaFieldValue(string json, string metafieldAlias);
    }
}
