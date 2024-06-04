using Ekom.Models.Import;

namespace Ekom.Services;

public interface IImportService
{
    /// <summary>
    /// Performs a full synchronization of the import data, updating categories, products, and their relationships based on the provided data structure.
    /// This operation can potentially affect the entire catalog, depending on the scope defined by the ParentKey.
    /// </summary>
    /// <param name="data">The complete set of import data representing the categories and products to be synchronized.</param>
    /// <param name="parentKey">Optional GUID of the parent category under which the data should be synchronized. If null, synchronization is performed at the root level.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public Task FullSyncAsync(ImportData data, Guid? parentKey = null, int syncUser = -1);

    /// <summary>
    /// Synchronizes a single category and its related products and subcategories. This method focuses on integrating or updating a specific category branch within the catalog.
    /// </summary>
    /// <param name="data">The complete set of import data representing the categories and products to be synchronized.</param>
    /// <param name="categoryKey">GUID of the parent category under which the data should be synchronized.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public Task CategorySyncAsync(ImportData data, Guid categoryKey, int syncUser = -1);

    /// <summary>
    /// Synchronizes a single product, ensuring it is updated or integrated into the catalog according to the provided data. This method is targeted at product-level operations.
    /// </summary>
    /// <param name="productData">The product data to be synchronized, including any variants and associated information.</param>
    /// <param name="parentKey">Optional GUID of the parent category under which the data should be synchronized. If null, synchronization is performed at the root level.</param>
    /// <param name="mediaRootKey">The GUID key identifying the media root folder in the CMS.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public Task ProductSyncAsync(ImportProduct productData, Guid? parentKey, Guid mediaRootKey,int syncUser = -1);


    public Task ProductUpdateSyncAsync(ImportProduct importProduct, Guid? parentKey, int syncUser = -1);

    public Task VariantUpdateSyncAsync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1);

}
