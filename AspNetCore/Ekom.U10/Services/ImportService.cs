using Ekom.API;
using Ekom.Events;
using Ekom.Models.Import;
using Ekom.Services;
using Ekom.Umb.Utilities;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using static Ekom.Events.ImportEvents;

namespace Ekom.Umb.Services;

public class ImportService : IImportService
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IScopeProvider _scopeProvider;
    private readonly ImportImageService _importImageService;
    private readonly ILogger<ImportService> _logger;
    private readonly Stock _stock;

    private IContentType? productContentType;
    private IContentType? productVariantGroupContentType;
    private IContentType? productVariantContentType;
    private IContentType? categoryContentType;
    private IContentType? catalogContentType;
    private IContent? umbracoRootContent;

    public ImportService(
        IUmbracoContextFactory umbracoContextFactory,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IScopeProvider scopeProvider,
        ILogger<ImportService> logger,
        Stock stock,
        ImportImageService importImageService)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _scopeProvider = scopeProvider;
        _logger = logger;
        _stock = stock;
        _importImageService = importImageService;
    }

    /// <summary>
    /// Performs a full synchronization process for imported data, including creating or updating content within the CMS.
    /// This process involves initializing essential data based on the provided parent key, iterating through category trees from the imported data,
    /// and ensuring the content reflects the current state of the import.
    /// </summary>
    /// <param name="data">The imported data containing the categories and other relevant information to be synchronized.</param>
    /// <param name="parentKey">Optional. The GUID key representing the parent content under which the synchronization should occur. If not provided, the method defaults to a pre-defined or root content based on implementation.</param>
    /// <param name="syncUser">The user ID to associate with the synchronization process. Defaults to -1, indicating an unspecified or system user.</param>
    /// <param name="identiferPropertyAlias">The property alias used to identify unique content items during the sync process. Defaults to "sku".</param>
    public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Full Sync running. ParentKey: {(parentKey.HasValue ? parentKey.Value.ToString() : "None")}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias} Categories: {data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()} Products: {data.Products.Count}");

        var stopwatchTotal = Stopwatch.StartNew();

        GetInitialData(parentKey);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importImageService.GetRootMedia(data.MediaRootKey);

        var allUmbracoMedia = _importImageService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        var stopwatch = Stopwatch.StartNew();

        using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            IterateCategoryTree(data.Categories, allUmbracoCategories, allUmbracoMedia, umbracoRootContent, identiferPropertyAlias, syncUser);

            _logger.LogInformation("IterateCategoryTree took {Duration} ms", stopwatch.ElapsedMilliseconds);

            stopwatch.Stop();

            stopwatch.Restart();

            IterateProductTree(data.Products, allUmbracoCategories, allUmbracoMedia, identiferPropertyAlias, syncUser);

            _logger.LogInformation("IterateProductTree took {Duration} ms", stopwatch.ElapsedMilliseconds);

            stopwatch.Stop();
        }

        stopwatchTotal.Stop();

        _logger.LogInformation("Full Sync took {Duration} ms", stopwatchTotal.ElapsedMilliseconds);
    }

    /// <summary>
    /// Synchronizes the given import category with the category identified by the provided category key in the CMS.
    /// This includes logging the synchronization process, fetching initial data, validating the root content,
    /// iterating through the category tree for any subcategories, and updating the category if there are content changes.
    /// </summary>
    /// <param name="importCategory">The import category data to be synchronized, including any subcategories and related data.</param>
    /// <param name="categoryKey">The GUID key identifying the category in the CMS. This key is used to fetch and update the corresponding category.</param>
    /// <param name="syncUser">Optional. The user ID associated with the synchronization process. Defaults to -1, indicating an unspecified or system user.</param>
    /// <param name="identiferPropertyAlias">The property alias used to identify unique elements within the category in the CMS. Defaults to "sku".</param>
    public void CategorySync(ImportCategory importCategory, Guid mediaRootKey, Guid categoryKey, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Category Sync running. CategoryKey: {categoryKey}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias}");

        GetInitialData(categoryKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importImageService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importImageService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        IterateCategoryTree(importCategory.SubCategories, allUmbracoCategories, allUmbracoMedia, umbracoRootContent, identiferPropertyAlias, syncUser);

        SaveCategory(umbracoRootContent, importCategory, allUmbracoMedia, false, syncUser);

        _logger.LogInformation("Category Sync finished CategoryKey: {categoryKey}", categoryKey.ToString());
    }

    /// <summary>
    /// Synchronizes the given import product with the product identified by the provided product key in the CMS. 
    /// This process includes logging the start and completion of the synchronization, retrieving initial data based on the product key,
    /// ensuring the root content is not null, and saving the product.
    /// </summary>
    /// <param name="importProduct">The import product data to be synchronized.</param>
    /// <param name="productKey">The GUID key identifying the product in the CMS. Used to fetch and update the corresponding product.</param>
    /// <param name="syncUser">Optional. The user ID associated with the synchronization process. Defaults to -1, indicating an unspecified or system user.</param>
    /// <param name="identiferPropertyAlias">The property alias used to identify the product within the CMS. Defaults to "sku".</param>
    public void ProductSync(ImportProduct importProduct, Guid productKey, Guid mediaRootKey, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Category Sync running. ProductKey: {productKey}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias}");

        GetInitialData(productKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importImageService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importImageService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        SaveProduct(umbracoRootContent, importProduct, allUmbracoCategories, allUmbracoMedia, false, syncUser);

        _logger.LogInformation("Product Sync finished ProductKey: {productKey}", productKey.ToString());
    }

    private void IterateCategoryTree(List<ImportCategory>? importCategories, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, IContent? parentContent, string identiferPropertyAlias, int syncUser)
    {

        if (parentContent == null)
        {
            return;
        }

        // Delete Categories

        // Create a HashSet of identifiers from importCategory for efficient lookups
        var importCategoryIdentifiers = importCategories == null ? new HashSet<string>() : new HashSet<string>(importCategories.Select(x => x.Identifier));

        // Delete Category not present in the importCategoryIdentifiers
        for (int i = allUmbracoCategories.Count - 1; i >= 0; i--)
        {
            var umbracoCategory = allUmbracoCategories[i];
            if (umbracoCategory.ParentId == parentContent.Id)
            {
                var categoryIdentifier = umbracoCategory.GetValue<string>(identiferPropertyAlias) ?? "";
                if (!importCategoryIdentifiers.Contains(categoryIdentifier))
                {
                    _logger.LogInformation($"Delete category Id: {umbracoCategory.Id} Name: {umbracoCategory.Name} Identifier: {categoryIdentifier}");

                    _contentService.Delete(umbracoCategory);
                    allUmbracoCategories.RemoveAt(i); // Remove from list
                }
            }
        }


        if (importCategories != null || importCategories?.Count > 0)
        {
            foreach (var importCategory in importCategories)
            {
                var umbracoChildrenContent = allUmbracoCategories.Where(x => x.ParentId == parentContent.Id).ToList();

                var content = GetOrCreateContent(categoryContentType, umbracoChildrenContent, importCategory.NodeName, importCategory.Identifier, parentContent, out bool create, identiferPropertyAlias);

                if (create)
                {
                    allUmbracoCategories.Add(content);
                }

                var save = create;

                SaveCategory(content, importCategory, allUmbracoMedia, create, syncUser);

                IterateCategoryTree(importCategory.SubCategories, allUmbracoCategories, allUmbracoMedia, content, identiferPropertyAlias, syncUser);
            }
        }

    }

    private void IterateProductTree(List<ImportProduct> importProducts, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, string identiferPropertyAlias, int syncUser)
    {
        ArgumentNullException.ThrowIfNull(categoryContentType);
        ArgumentNullException.ThrowIfNull(productContentType);
        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        if (importProducts != null || importProducts?.Count > 0)
        {
            var allUmbracoProducts = _contentService
                .GetPagedOfType(productContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                .Where(x => !x.Trashed & x.Path.Contains(umbracoRootContent.Id.ToString())))
                .ToList();


            // Create a HashSet of identifiers from importProducts for efficient lookups
            var importProductIdentifiers = new HashSet<string>(importProducts.Select(x => x.Identifier));

            // HashSet to track identifiers already processed
            var processedIdentifiers = new HashSet<string>();

            // Delete Products not present in the importProductIdentifiers or that are duplicates
            for (int i = allUmbracoProducts.Count - 1; i >= 0; i--)
            {
                var umbracoProduct = allUmbracoProducts[i];
                var productIdentifier = umbracoProduct.GetValue<string>(identiferPropertyAlias) ?? "";

                // Check if the identifier is valid and not in the import list
                if (!importProductIdentifiers.Contains(productIdentifier))
                {
                    _logger.LogInformation($"Product deleted Id: {umbracoProduct.Id} Sku: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} ProductIdentifier: {productIdentifier}");
                    _contentService.Delete(umbracoProduct);
                    allUmbracoProducts.RemoveAt(i);
                }
                else if (!processedIdentifiers.Add(productIdentifier)) // Try to add to processed, fails if already present
                {
                    // Duplicate found, delete the duplicate item
                    _logger.LogInformation($"Duplicate product deleted Id: {umbracoProduct.Id} Sku: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId}");
                    _contentService.Delete(umbracoProduct);
                    allUmbracoProducts.RemoveAt(i);
                }
            }

            foreach (var importProduct in importProducts)
            {
                if (importProduct.Categories.Count > 0)
                {
                    var primaryCategoryContent = allUmbracoCategories.FirstOrDefault(x => x.GetValue<string>(identiferPropertyAlias) == importProduct.Categories.FirstOrDefault());

                    if (primaryCategoryContent != null)
                    {
                        var umbracoChildrenContent = allUmbracoProducts.Where(x => x.ParentId == primaryCategoryContent.Id).ToList();

                        var content = GetOrCreateContent(productContentType, umbracoChildrenContent, importProduct.NodeName, importProduct.Identifier, primaryCategoryContent, out bool create, identiferPropertyAlias);

                        var save = create;

                        SaveProduct(content, importProduct, allUmbracoCategories, allUmbracoMedia, create, syncUser);
                    }
                }

            }
        }

    }

    private void SaveCategory(IContent categoryContent, ImportCategory importCategory, List<IMedia> allUmbracoMedia, bool create, int syncUser)
    {
        OnCategorySaveStarting(this, new ImportCategoryEventArgs(categoryContent, importCategory, create));

        var saveImage = ImportImage(categoryContent, importCategory.Images, allUmbracoMedia);

        var compareValue = importCategory.Comparer ?? ComputeSha256Hash(importCategory, new string[] { "SubCategories", "Products", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(categoryContent.GetValue<string>("comparer"), compareValue) && !create && !saveImage)
        {
            return;
        }

        categoryContent.SetProperty("title", importCategory.Title);

        if (importCategory.Slug != null && importCategory.Slug.Any())
        {
            categoryContent.SetSlug(importCategory.Slug);
        }

        if (!string.IsNullOrEmpty(importCategory.SKU))
        {
            categoryContent.SetValue("sku", importCategory.SKU);
        }

        categoryContent.SetProperty("description", importCategory.Description);


        if (importCategory.IdentiferPropertyAlias != "sku")
        {
            categoryContent.SetValue(importCategory.IdentiferPropertyAlias, importCategory.Identifier);
        }

        if (importCategory.AdditionalProperties != null && importCategory.AdditionalProperties.Any())
        {
            foreach (var property in importCategory.AdditionalProperties)
            {
                categoryContent.SetValue(property.Key, property.Value);
            }
        }

        categoryContent.SetValue("comparer", compareValue);

        categoryContent.Name = importCategory.NodeName;

        if (importCategory.TemplateId.HasValue)
        {
            categoryContent.TemplateId = importCategory.TemplateId.Value;
        }

        using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            if (categoryContent.Published || create)
            {
                _contentService.SaveAndPublish(categoryContent, userId: syncUser);
            }
            else
            {
                _contentService.Save(categoryContent, userId: syncUser);
            }
        }
    }

    private void SaveProduct(IContent productContent, ImportProduct importProduct, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, bool create, int syncUser)
    {
        OnProductSaveStarting(this, new ImportProductEventArgs(productContent, importProduct, create));

        // Always do stock update
        if (importProduct.Stock.Any())
        {
            foreach (var stock in importProduct.Stock)
            {
                var currentStock = _stock.GetStock(productContent.Key);
                var newStock = stock.Stock >= 0 ? stock.Stock : 0;

                // Only update if we find change 
                if (newStock != currentStock)
                {
                    var stockUpdated = _stock.SetStockAsync(productContent.Key, stock.StoreAlias, stock.Stock).Result;
                }
            }
        }

        var saveImage = ImportImage(productContent, importProduct.Images, allUmbracoMedia);

        var compareValue = importProduct.Comparer ?? ComputeSha256Hash(importProduct, new string[] { "VariantGroups", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(productContent.GetValue<string>("comparer"), compareValue) && !create && !saveImage)
        {
            return;
        }

        productContent.SetProperty("title", importProduct.Title);

        if (importProduct.Slug != null && importProduct.Slug.Any())
        {
            productContent.SetSlug(importProduct.Slug);
        }

        if (!string.IsNullOrEmpty(importProduct.SKU))
        {
            productContent.SetValue("sku", importProduct.SKU);
        }

        productContent.SetProperty("description", importProduct.Description);


        if (importProduct.IdentiferPropertyAlias != "sku")
        {
            productContent.SetValue(importProduct.IdentiferPropertyAlias, importProduct.Identifier);
        }

        if (importProduct.Price.Any())
        {
            foreach (var price in importProduct.Price)
            {
                productContent.SetPrice(price.StoreAlias, price.Currency, price.Price);
            }
        }

        if (importProduct.AdditionalProperties != null && importProduct.AdditionalProperties.Any())
        {
            foreach (var property in importProduct.AdditionalProperties)
            {
                productContent.SetValue(property.Key, property.Value);
            }
        }

        if (importProduct.Categories.Count > 1)
        {
            var umbracoCategories = allUmbracoCategories.Where(x => importProduct.Categories.Skip(1).Contains(x.GetValue<string>(importProduct.IdentiferPropertyAlias)));

            var udis = umbracoCategories.Select(x => x.GetUdi());

            var stringUdis = string.Join(",", udis.Select(x => x.ToString()));

            productContent.SetValue("categories", stringUdis);
        }

        productContent.SetValue("comparer", compareValue);

        productContent.Name = importProduct.NodeName;

        if (importProduct.TemplateId.HasValue)
        {
            productContent.TemplateId = importProduct.TemplateId.Value;
        }

        using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            if (productContent.Published || create)
            {
                _contentService.SaveAndPublish(productContent, userId: syncUser);
            }
            else
            {
                _contentService.Save(productContent, userId: syncUser);
            }
        }
    }

    private bool ImportImage(IContent content, List<IImportImage> images, List<IMedia> allUmbracoMedia)
    {
        var imagesUdi = new List<string>();

        foreach (var image in images)
        {
            if (image is ImportImageFromUdi importImage)
            {
                imagesUdi.Add(importImage.ImageUdi);
            }
            else if (image is ImportImageFromExternalUrl externalUrlImage)
            {
                var compareValue = externalUrlImage.Comparer ?? ComputeSha256Hash(externalUrlImage);

                var media = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (media == null)
                {
                    media = _importImageService.ImportImageFromExternalUrl(externalUrlImage, compareValue);
                    allUmbracoMedia.Add(media);
                }

                imagesUdi.Add(media.GetUdi().ToString());
            }
            else if (image is ImportImageFromBytes bytesImage)
            {
                var compareValue = bytesImage.Comparer ?? ComputeSha256Hash(bytesImage, new string[] { "ImageBytes" });

                var media = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (media == null)
                {
                    media = _importImageService.ImportImageFromBytes(bytesImage, compareValue);
                    allUmbracoMedia.Add(media);
                }

                imagesUdi.Add(media.GetUdi().ToString());
            }
            else if (image is ImportImageFromBase64 base64Image) {

                var compareValue = base64Image.Comparer ?? ComputeSha256Hash(base64Image, new string[] { "ImageBase64" });

                var media = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (media == null)
                {
                    media = _importImageService.ImportImageFromBase64(base64Image, compareValue);
                    allUmbracoMedia.Add(media);
                }

                imagesUdi.Add(media.GetUdi().ToString());
            }
        }

        var currentImages = content.GetValue<string>("images") ?? "";
        var importedImages = string.Join(",", imagesUdi);

        if (currentImages != importedImages)
        {
            content.SetValue("images", importedImages);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Initializes critical content type data from a content service based on an optional parent key.
    /// Retrieves various content types (e.g., category, product, catalog) from the content service
    /// and optionally fetches the root content based on the provided parent key.
    /// </summary>
    /// <param name="parentKey">Optional GUID key representing the parent content. If provided, the method attempts to fetch the root content using this key. If not provided, it fetches the first untrashed catalog content.</param>
    /// <remarks>
    /// This method relies on `_contentTypeService` and `_contentService` being properly initialized.
    /// It throws an exception if any of the content types or the root content cannot be retrieved.
    /// </remarks>
    private void GetInitialData(Guid? parentKey)
    {
        // Initialize content types from the content service
        categoryContentType = _contentTypeService.Get("ekmCategory");
        productContentType = _contentTypeService.Get("ekmProduct");
        catalogContentType = _contentTypeService.Get("ekmCatalog");
        productVariantGroupContentType = _contentTypeService.Get("ekmProductVariantGroup");
        productVariantContentType = _contentTypeService.Get("ekmProductVariant");

        // Validate that all content types have been successfully retrieved
        ArgumentNullException.ThrowIfNull(categoryContentType);
        ArgumentNullException.ThrowIfNull(productContentType);
        ArgumentNullException.ThrowIfNull(catalogContentType);
        ArgumentNullException.ThrowIfNull(productVariantGroupContentType);
        ArgumentNullException.ThrowIfNull(productVariantContentType);

        // Retrieve the root content based on the provided parentKey or fetch the first untrashed catalog content
        if (parentKey.HasValue)
        {
            umbracoRootContent = _contentService.GetById(parentKey.Value);
        }
        else
        {
            umbracoRootContent = _contentService
                .GetPagedOfType(catalogContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                .Where(x => !x.Trashed)).FirstOrDefault();
        }

        // Ensure the root content has been successfully retrieved
        ArgumentNullException.ThrowIfNull(umbracoRootContent);
    }

    /// <summary>
    /// Gets an existing content item from a list of Umbraco content by its identifier or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="contenType">The content type to create</param>
    /// <param name="umbracoChildrenContent">The list of child content items to search through.</param>
    /// <param name="nodeName">The name for the new content node if creation is needed.</param>
    /// <param name="identifer">The identifier to search for in the existing content items. Cannot be null.</param>
    /// <param name="parentContent">The parent content under which the new content should be created if needed.</param>
    /// <param name="create">Outputs true if a new content item was created, false otherwise.</param>
    /// <param name="identiferPropertyAlias">The property alias to search the identifier under. Defaults to "sku".</param>
    /// <returns>The found or newly created content item.</returns>
    private IContent GetOrCreateContent(IContentType? contenType, List<IContent> umbracoChildrenContent, string nodeName, string identifer, IContent parentContent, out bool create, string identiferPropertyAlias = "sku")
    {
        ArgumentNullException.ThrowIfNull(contenType);
        ArgumentNullException.ThrowIfNull(nodeName);
        ArgumentNullException.ThrowIfNull(identifer);

        create = false;
        var content = umbracoChildrenContent.FirstOrDefault(x => x.GetValue<string>(identiferPropertyAlias) == identifer);

        if (content == null)
        {
            // Note: Assuming 'Content' is a constructor for an object that implements IContent
            // and 'categoryContentType' is defined elsewhere in your class.
            content = new Content(nodeName, parentContent.Id, contenType);
            create = true;
        }

        return content;
    }

    /// <summary>
    /// Determines whether there are any changes between the existing and new comparer values.
    /// </summary>
    /// <param name="existingComparerValue">The existing value to compare. Can be null.</param>
    /// <param name="newComparerValue">The new value to compare against the existing value.</param>
    /// <returns>True if there are changes between the existing and new values; otherwise, false.</returns>
    private bool HasContentChanges(string? existingComparerValue, string newComparerValue)
    {
        return existingComparerValue != newComparerValue;
    }

    private string ComputeSha256Hash(object o, string[]? propertiesToIgnore = null)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new ImportSerializeContractResolver()
        };

        if (propertiesToIgnore != null)
        {
            foreach (var propertyName in propertiesToIgnore)
            {
                ((ImportSerializeContractResolver)settings.ContractResolver).IgnorePropertyByName(propertyName);
            }
        }

        var json = JsonConvert.SerializeObject(o, settings);

        // Create a SHA256
        using (var sha256Hash = SHA256.Create())
        {
            // ComputeHash - returns byte array
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(json));

            // Convert byte array to a string
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private List<IContent> GetAllUmbracoCategories()
    {
        ArgumentNullException.ThrowIfNull(categoryContentType);
        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var categories =  _contentService
            .GetPagedOfType(categoryContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed & x.Path.Contains(umbracoRootContent.Id.ToString())))
            .ToList();

        var filteredContents = categories
        .Where(x => x.Path.Split(',').Contains(umbracoRootContent.Id.ToString()))
        .ToList();

        return filteredContents;
    }
}
