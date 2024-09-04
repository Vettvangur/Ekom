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
    public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1);

    /// <summary>
    /// Synchronizes a single category and its related products and subcategories. This method focuses on integrating or updating a specific category branch within the catalog.
    /// </summary>
    /// <param name="data">The complete set of import data representing the categories and products to be synchronized.</param>
    /// <param name="categoryKey">GUID of the parent category under which the data should be synchronized.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void CategorySync(ImportData data, Guid categoryKey, int syncUser = -1);

    /// <summary>
    /// Synchronizes a single product, ensuring it is updated or integrated into the catalog according to the provided data. This method is targeted at product-level operations.
    /// </summary>
    /// <param name="productData">The product data to be synchronized, including any variants and associated information.</param>
    /// <param name="parentKey">Optional GUID of the parent category under which the data should be synchronized. If null, synchronization is performed at the root level.</param>
    /// <param name="mediaRootKey">The GUID key identifying the media root folder in the CMS.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void ProductSync(ImportProduct productData, Guid? parentKey, Guid mediaRootKey, int syncUser = -1);

    /// <summary>
    /// Updates a single product, ensuring the product data is modified or synchronized in the catalog.
    /// </summary>
    /// <param name="importProduct">The product data to be updated.</param>
    /// <param name="parentKey">Optional GUID of the parent category under which the data should be updated. If null, the product is updated at the root level.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void ProductUpdateSync(ImportProduct importProduct, Guid? parentKey, int syncUser = -1);

    /// <summary>
    /// Updates a single variant, ensuring the variant data is modified or synchronized in the catalog.
    /// </summary>
    /// <param name="importVariant">The variant data to be updated.</param>
    /// <param name="parentKey">Optional GUID of the parent category under which the variant should be updated.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void VariantUpdateSync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1);

    /// <summary>
    /// Synchronizes a variant group, updating or integrating its details and associated variants within the catalog.
    /// </summary>
    /// <param name="importVariantGroup">The variant group data to be synchronized.</param>
    /// <param name="parentKey">The GUID of the parent product under which the variant group should be synchronized.</param>
    /// <param name="mediaRootKey">The GUID key identifying the media root folder in the CMS.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void VariantGroupSync(ImportVariantGroup importVariantGroup, Guid parentKey, Guid mediaRootKey, int syncUser = -1);

    /// <summary>
    /// Synchronizes a single variant, ensuring it is updated or integrated into the catalog based on the provided data.
    /// </summary>
    /// <param name="importVariant">The variant data to be synchronized.</param>
    /// <param name="parentKey">The GUID of the parent variant Group under which the variant should be synchronized.</param>
    /// <param name="mediaRootKey">The GUID key identifying the media root folder in the CMS.</param>
    /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
    public void VariantSync(ImportVariant importVariant, Guid parentKey, Guid mediaRootKey, int syncUser = -1);
}
