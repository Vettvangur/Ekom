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
    private readonly ImportMediaService _importMediaService;
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
        ImportMediaService importMediaService)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _scopeProvider = scopeProvider;
        _logger = logger;
        _stock = stock;
        _importMediaService = importMediaService;
    }

    public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Full Sync running. ParentKey: {(parentKey.HasValue ? parentKey.Value.ToString() : "None")}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias} Categories: {data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()} Products: {data.Products.Count}");

        var stopwatchTotal = Stopwatch.StartNew();

        GetInitialData(parentKey);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

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

    public void CategorySync(ImportData data, Guid categoryKey, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Category Sync running. CategoryKey: {categoryKey}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias}");

        GetInitialData(categoryKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        IterateCategoryTree(data.Categories, allUmbracoCategories, allUmbracoMedia, umbracoRootContent, identiferPropertyAlias, syncUser);

        if (data.Products != null && data.Products.Any())
        {
            IterateProductTree(data.Products, allUmbracoCategories, allUmbracoMedia, identiferPropertyAlias, syncUser);
        }

        _logger.LogInformation("Category Sync finished CategoryKey: {categoryKey}", categoryKey.ToString());
    }

    public void ProductSync(ImportProduct importProduct, Guid productKey, Guid mediaRootKey, int syncUser = -1, string identiferPropertyAlias = "sku")
    {
        _logger.LogInformation($"Category Sync running. ProductKey: {productKey}, SyncUser: {syncUser}, Identifier: {identiferPropertyAlias}");

        GetInitialData(productKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allUmbracoCategories = GetAllUmbracoCategories();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        SaveProduct(umbracoRootContent, importProduct, allUmbracoCategories, allUmbracoMedia, false, identiferPropertyAlias, syncUser);

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

                SaveCategory(content, importCategory, allUmbracoMedia, create, identiferPropertyAlias, syncUser);

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
            var ekomCatalogContent = _contentService
            .GetPagedOfType(catalogContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed)).FirstOrDefault();

            ArgumentNullException.ThrowIfNull(ekomCatalogContent);

            var allEkomNodes = _contentService
                .GetPagedDescendants(ekomCatalogContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                .Where(x => !x.Trashed & x.Path.Contains(umbracoRootContent.Id.ToString())))
                .Where(x => !x.GetValue<bool>("ekmDisableSync")).ToList();

            var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

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
                    _logger.LogInformation($"Product deleted Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} ProductIdentifier: {productIdentifier}");
                    _contentService.Delete(umbracoProduct);
                    allUmbracoProducts.RemoveAt(i);
                }
                else if (!processedIdentifiers.Add(productIdentifier)) // Try to add to processed, fails if already present
                {
                    // Duplicate found, delete the duplicate item
                    _logger.LogInformation($"Duplicate product deleted Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId}");
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

                        SaveProduct(content, importProduct, allUmbracoCategories, allUmbracoMedia, create, identiferPropertyAlias, syncUser);

                        IterateVariantGroups(importProduct, content, allEkomNodes, allUmbracoMedia, identiferPropertyAlias, syncUser);
                    }
                }

            }
        }

    }

    private void IterateVariantGroups(ImportProduct importProduct, IContent productContent, List<IContent> allEkomNodes, List<IMedia> allUmbracoMedia, string identiferPropertyAlias, int syncUser)
    {
        var umbracoVariantGroupChildrenContent = allEkomNodes.Where(x => x.ParentId == productContent.Id).ToList();

        // Delete Variant Groups

        // Create a HashSet of identifiers from importVariantGroup for efficient lookups
        var importVariantGroupsIdentifiers = new HashSet<string>(importProduct.VariantGroups.Select(x => x.Identifier));

        // Delete VariantGroup not present
        for (int i = umbracoVariantGroupChildrenContent.Count - 1; i >= 0; i--)
        {
            var umbracoVariantGroup = umbracoVariantGroupChildrenContent[i];
       
            var variantGroupIdentifier = umbracoVariantGroup.GetValue<string>(identiferPropertyAlias) ?? "";
            if (!importVariantGroupsIdentifiers.Contains(variantGroupIdentifier))
            {
                _logger.LogInformation($"Delete variant Group Id: {umbracoVariantGroup.Id} Name: {umbracoVariantGroup.Name} Identifier: {variantGroupIdentifier}");

                _contentService.Delete(umbracoVariantGroup);
                allEkomNodes.RemoveAt(i); // Remove from list
                umbracoVariantGroupChildrenContent.RemoveAt(i);
            }
            
        }

        foreach (var importVariantGroup in importProduct.VariantGroups)
        {
            var content = GetOrCreateContent(productVariantGroupContentType, umbracoVariantGroupChildrenContent, importVariantGroup.NodeName, importVariantGroup.Identifier, productContent, out bool create, identiferPropertyAlias);
            
            var save = create;

            SaveVariantGroup(content, importVariantGroup, allUmbracoMedia, create, identiferPropertyAlias, syncUser);
        }
    }

    private void SaveCategory(IContent categoryContent, ImportCategory importCategory, List<IMedia> allUmbracoMedia, bool create, string identiferPropertyAlias, int syncUser)
    {
        OnCategorySaveStarting(this, new ImportCategoryEventArgs(categoryContent, importCategory, create));

        var saveImages = ImportMedia(categoryContent, importCategory.Images, allUmbracoMedia, "files");

        var compareValue = importCategory.Comparer ?? ComputeSha256Hash(importCategory, new string[] { "SubCategories", "Images", "Products", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(categoryContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages)
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
            categoryContent.SetValue(identiferPropertyAlias != importCategory.IdentiferPropertyAlias ? importCategory.IdentiferPropertyAlias : identiferPropertyAlias, importCategory.Identifier);
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

    private void SaveProduct(IContent productContent, ImportProduct importProduct, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, bool create, string identiferPropertyAlias, int syncUser)
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

        var saveImages = ImportMedia(productContent, importProduct.Images, allUmbracoMedia);

        var saveFiles = ImportMedia(productContent, importProduct.Files, allUmbracoMedia, "File", "files");

        var compareValue = importProduct.Comparer ?? ComputeSha256Hash(importProduct, new string[] { "VariantGroups", "Images", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(productContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages && !saveFiles)
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
            productContent.SetValue(identiferPropertyAlias != importProduct.IdentiferPropertyAlias ? importProduct.IdentiferPropertyAlias : identiferPropertyAlias, importProduct.Identifier);
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
    private void SaveVariantGroup(IContent variantGroupContent, ImportVariantGroup importVariantGroup, List<IMedia> allUmbracoMedia, bool create, string identiferPropertyAlias, int syncUser)
    {
        var saveImages = ImportMedia(variantGroupContent, importVariantGroup.Images, allUmbracoMedia);

        var compareValue = importVariantGroup.Comparer ?? ComputeSha256Hash(importVariantGroup, new string[] { "Variants", "Images", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(variantGroupContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages)
        {
            return;
        }

        variantGroupContent.SetProperty("title", importVariantGroup.Title);
        variantGroupContent.SetValue(identiferPropertyAlias != importVariantGroup.IdentiferPropertyAlias ? importVariantGroup.IdentiferPropertyAlias : identiferPropertyAlias, importVariantGroup.Identifier);
        

        if (importVariantGroup.AdditionalProperties != null && importVariantGroup.AdditionalProperties.Any())
        {
            foreach (var property in importVariantGroup.AdditionalProperties)
            {
                variantGroupContent.SetValue(property.Key, property.Value);
            }
        }

        variantGroupContent.SetValue("comparer", compareValue);

        variantGroupContent.Name = importVariantGroup.NodeName;

        using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            if (variantGroupContent.Published || create)
            {
                _contentService.SaveAndPublish(variantGroupContent, userId: syncUser);
            }
            else
            {
                _contentService.Save(variantGroupContent, userId: syncUser);
            }
        }
    }

    private bool ImportMedia(IContent content, List<IImportMedia> importMedias, List<IMedia> allUmbracoMedia, string mediaType = "Image", string contentTypeAlias = "images")
    {
        var imagesUdi = new List<string>();

        foreach (var media in importMedias)
        {
            if (media is ImportMediaFromUdi importMedia)
            {
                imagesUdi.Add(importMedia.Udi);
            }
            else if (media is ImportMediaFromExternalUrl externalUrlMedia)
            {
                var compareValue = externalUrlMedia.Comparer ?? ComputeSha256Hash(externalUrlMedia);

                var umbMedia = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType);
                    allUmbracoMedia.Add(umbMedia);
                }

                imagesUdi.Add(umbMedia.GetUdi().ToString());
            }
            else if (media is ImportMediaFromBytes bytesMedia)
            {
                var compareValue = bytesMedia.Comparer ?? ComputeSha256Hash(bytesMedia, new string[] { "Bytes" });

                var umbMedia = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBytes(bytesMedia, compareValue, mediaType);
                    allUmbracoMedia.Add(umbMedia);
                }

                imagesUdi.Add(umbMedia.GetUdi().ToString());
            }
            else if (media is ImportMediaFromBase64 base64Media) {

                var compareValue = base64Media.Comparer ?? ComputeSha256Hash(base64Media, new string[] { "Base64" });

                var umbMedia = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBase64(base64Media, compareValue, mediaType);
                    allUmbracoMedia.Add(umbMedia);
                }

                imagesUdi.Add(umbMedia.GetUdi().ToString());
            }
        }

        var currentImages = content.GetValue<string>(contentTypeAlias) ?? "";
        var importedImages = string.Join(",", imagesUdi);

        if (currentImages != importedImages)
        {
            content.SetValue(contentTypeAlias, importedImages);
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

        var categories = _contentService
            .GetPagedOfType(categoryContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed && x.Path.Contains(umbracoRootContent.Id.ToString())))
            .Where(x => !x.GetValue<bool>("ekmDisableSync") && x.Path.Split(',').Contains(umbracoRootContent.Id.ToString()))
            .ToList();

        return categories;
    }
}
