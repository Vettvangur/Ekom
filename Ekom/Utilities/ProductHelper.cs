using Ekom.API;
using Ekom.Models;
using Ekom.Services;

namespace Ekom.Utilities
{
    internal static class ProductHelper
    {
        internal static IEnumerable<IProduct> GetProducts(string udis, string storeAlias = null)
        {
            if (!string.IsNullOrEmpty(udis) && udis.StartsWith("umb"))
            {
                var result = new List<IProduct>();


                if (UtilityService.ConvertUdisToGuids(udis, out IEnumerable<Guid> guids))
                {
                    foreach (var guid in guids)
                    {
                        var product = Catalog.Instance.GetProduct(storeAlias, guid);

                        if (product != null)
                        {
                            result.Add(product);
                        }
                    }
                }

                return result;
            }

            return Enumerable.Empty<IProduct>();

        }
        internal static IProduct GetProduct(string udi, string storeAlias = null)
        {
            if (!string.IsNullOrEmpty(udi) && udi.StartsWith("umb"))
            {
                if (UtilityService.ConvertUdiToGuid(udi, out Guid guid))
                {
                    var product = Catalog.Instance.GetProduct(storeAlias, guid);

                    if (product != null)
                    {
                        return product;
                    }
                }
            }

            return null;

        }
    }
}
