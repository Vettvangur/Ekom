using Ekom.Umb.CatalogCollection.Models;

namespace Ekom.Umb.CatalogCollection.Services;

public interface ICatalogCollectionService
{
    CatalogCollectionResponse GetCollection(string nodeId, CatalogCollectionRequest request);
}
