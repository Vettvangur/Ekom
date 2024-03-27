using Ekom.Models.Import;

namespace Ekom.Services
{
    public interface IImportService
    {
        /// <summary>
        /// Performs a full synchronization of the import data, updating categories, products, and their relationships based on the provided data structure.
        /// This operation can potentially affect the entire catalog, depending on the scope defined by the ParentKey.
        /// </summary>
        /// <param name="data">The complete set of import data representing the categories and products to be synchronized.</param>
        /// <param name="parentKey">Optional GUID of the parent category under which the data should be synchronized. If null, synchronization is performed at the root level.</param>
        /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
        /// <param name="identiferPropertyAlias">Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.</param>
        public void FullSync(ImportData data, Guid? parentKey, int syncUser = -1, string identiferPropertyAlias = "sku");

        /// <summary>
        /// Synchronizes a single category and its related products and subcategories. This method focuses on integrating or updating a specific category branch within the catalog.
        /// </summary>
        /// <param name="categoryData">The category data to be synchronized, including any subcategories and products it contains.</param>
        /// <param name="parentKey">Optional GUID of the parent category under which the category data should be synchronized. If null, the category is treated as a top-level category.</param>
        /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
        /// <param name="identiferPropertyAlias">Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.</param>
        public void CategorySync(ImportCategory categoryData, Guid? parentKey, int syncUser = -1, string identiferPropertyAlias = "sku");

        /// <summary>
        /// Synchronizes a single product, ensuring it is updated or integrated into the catalog according to the provided data. This method is targeted at product-level operations.
        /// </summary>
        /// <param name="productData">The product data to be synchronized, including any variants and associated information.</param>
        /// <param name="parentKey">Optional GUID of the parent category under which the product should be synchronized. Specifies where in the category hierarchy the product belongs.</param>
        /// <param name="syncUser">The user ID initiating the sync operation. Defaults to -1 to represent a system or anonymous user.</param>
        /// <param name="identiferPropertyAlias">Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.</param>
        public void ProductSync(ImportProduct productData, Guid? parentKey, int syncUser = -1, string identiferPropertyAlias = "sku");
    }

}
