using Ekom.API;
using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Import;
using Ekom.Services;
using Ekom.Umb.Utilities;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Infrastructure.Sync;
using Umbraco.Extensions;
using static Ekom.Events.ImportEvents;


namespace Ekom.Umb.Services;

public class ImportService : IImportService
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IScopeProvider _scopeProvider;
    private readonly IServerMessenger _serverMessenger;
    private readonly ImportMediaService _importMediaService;
    private readonly ILogger<ImportService> _logger;
    private readonly Stock _stock;
    private readonly INodeService _nodeService;

    private IContentType? productContentType;
    private IContentType? productVariantGroupContentType;
    private IContentType? productVariantContentType;
    private IContentType? categoryContentType;
    private IContentType? catalogContentType;
    private IContent? umbracoRootContent;

    private List<ImportCategory> categoriesSaved;
    private List<ImportProduct> productsSaved;
    private List<ImportVariant> variantsSaved;
    private List<ImportVariantGroup> variantGroupsSaved;

    private int categoriesDeleted = 0;
    private int productDeleted = 0;
    private int variantDeleted = 0;
    private int variantGroupDeleted = 0;

    private static readonly object _syncLock = new object();
    private static bool _isFullSyncRunning = false;

    public ImportService(
        IUmbracoContextFactory umbracoContextFactory,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IScopeProvider scopeProvider,
        IServerMessenger serverMessenger,
        ILogger<ImportService> logger,
        Stock stock,
        ImportMediaService importMediaService,
        INodeService nodeService)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _scopeProvider = scopeProvider;
        _serverMessenger = serverMessenger;
        _logger = logger;
        _stock = stock;
        _importMediaService = importMediaService;
        _nodeService = nodeService;
    }

    public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1)
    {
        _logger.LogInformation($"Full Sync running. ParentKey: {(parentKey.HasValue ? parentKey.Value.ToString() : "None")}, SyncUser: {syncUser}, Categories: {data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()} Products: {data.Products.Count}");

        lock (_syncLock)
        {
            if (_isFullSyncRunning)
            {
                _logger.LogError("Full Sync is already in progress.");
                throw new InvalidOperationException("Sync is already in progress.");
            }

            _isFullSyncRunning = true;
        }

        try
        {
            var stopwatchTotal = Stopwatch.StartNew();

            using var backgroundScope = new BackgroundScope(_serverMessenger);

            GetInitialData(parentKey);

            var allEkomNodes = GetAllEkomNodes();

            var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory" && x.Path.Contains(umbracoRootContent.Id.ToString(), StringComparison.InvariantCulture)).Where(x => x.Path.Split(',').Contains(umbracoRootContent.Id.ToString())).ToList();
            var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

            var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

            var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

            var stopwatch = Stopwatch.StartNew();

            IterateCategoryTree(data.Categories, allUmbracoCategories, allUmbracoMedia, umbracoRootContent, syncUser);

            _logger.LogInformation("IterateCategoryTree took {Duration} seconds", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"));

            stopwatch.Stop();

            stopwatch.Restart();

            IterateProductTree(data.Products, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, syncUser);

            _logger.LogInformation("IterateProductTree took {Duration} seconds", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"));

            stopwatch.Stop();

            OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.FullSync)).GetAwaiter().GetResult();

            stopwatchTotal.Stop();

            _logger.LogInformation(
                "Full Sync took {Duration} seconds. Categories Saved: {categoriesCount} Products Saved: {productsCount} Variants Saved: {variantsCount} VariantsGroups Saved: {variantGroupsCount} Categories Deleted: {categoriesDeleted} Products Deleted: {productDeleted} Variants Deleted: {variantDeleted} VariantsGroups Deleted: {variantGroupDeleted}", 
                (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"), categoriesSaved.Count, productsSaved.Count, variantsSaved.Count, variantGroupsSaved.Count, categoriesDeleted, productDeleted, variantDeleted, variantGroupDeleted);
        }
        finally
        {
            lock (_syncLock)
            {
                _isFullSyncRunning = false;
            }
        }
    }

    public void CategorySync(ImportData data, Guid parentKey, int syncUser = -1)
    {
        _logger.LogInformation($"Category Sync running. ParentKey: {parentKey}, SyncUser: {syncUser}, Categories: {data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()} Products: {data.Products.Count}");

        var stopwatch = Stopwatch.StartNew();

        using var backgroundScope = new BackgroundScope(_serverMessenger);

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory").ToList();
        var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        IterateCategoryTree(data.Categories, allUmbracoCategories, allUmbracoMedia, umbracoRootContent, syncUser, false);

        if (data.Products != null && data.Products.Any())
        {
            IterateProductTree(data.Products, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, syncUser);
        }

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.CategorySync)).GetAwaiter().GetResult();

        stopwatch.Stop();

        _logger.LogInformation(
            "Category Sync took {Duration} seconds. Parent {parentKey} Categories Saved: {categoriesCount} Products Saved: {productsCount} Variants Saved: {variantsCount} VariantsGroups Saved: {variantGroupsCount} Categories Deleted: {categoriesDeleted} Products Deleted: {productDeleted} Variants Deleted: {variantDeleted} VariantsGroups Deleted: {variantGroupDeleted}",
            (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"), parentKey, categoriesSaved.Count, productsSaved.Count, variantsSaved.Count, variantGroupsSaved.Count, categoriesDeleted, productDeleted, variantDeleted, variantGroupDeleted);
    }

    public void ProductSync(ImportProduct importProduct, Guid? parentKey, Guid mediaRootKey, int syncUser = -1)
    {
        _logger.LogInformation($"Product Sync running. SKU: {importProduct.SKU}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory").ToList();
        var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        IterateProductTree(new List<ImportProduct> { importProduct }, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, syncUser, false);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.ProductSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Sync finished ProductKey: {SKU}", importProduct.SKU);
    }
    public void VariantGroupSync(ImportVariantGroup importVariantGroup, Guid parentKey, Guid mediaRootKey, int syncUser = -1)
    {
        _logger.LogInformation($"Variant Group Sync running. SKU: {importVariantGroup.Identifier}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        var allEkomNodes = _contentService
            .GetPagedDescendants(umbracoRootContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed)).ToList();

        IterateVariantGroups(new List<ImportVariantGroup> { importVariantGroup }, umbracoRootContent, allEkomNodes, allUmbracoMedia, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantGroupSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Variant Group finished: {SKU}", importVariantGroup.Identifier);
    }
    public void VariantSync(ImportVariant importVariant, Guid parentKey, Guid mediaRootKey, int syncUser = -1)
    {
        _logger.LogInformation($"Variant Sync running. SKU: {importVariant.SKU}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        var allEkomNodes = _contentService
            .GetPagedDescendants(umbracoRootContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed)).ToList();

        IterateVariants(new List<ImportVariant> { importVariant }, umbracoRootContent, allEkomNodes, allUmbracoMedia, syncUser, false);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Variant finished: {SKU}", importVariant.SKU);
    }
    public void ProductUpdateSync(ImportProduct importProduct, Guid? parentKey, int syncUser = -1)
    {
        _logger.LogInformation($"Product Update Sync running. SKU: {importProduct.SKU}, SyncUser: {syncUser}");

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = _contentService
            .GetPagedDescendants(umbracoRootContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .Where(x => !x.GetValue<bool>("ekmDisableSync")).ToList();

        var product = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmProduct" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importProduct.Identifier);

        if (product == null)
        {
            throw new ArgumentNullException(nameof(product), $"Product is null. Identifier: {importProduct.Identifier} SKU: {importProduct.SKU} ParentKey: {parentKey}");
        }

        SaveProduct(product, importProduct, null, null, false, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.ProductUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Update Sync finished ProductKey: {SKU} ProductId: {Id}", importProduct.SKU, product.Id);
    }
    public void VariantUpdateSync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1)
    {
        ArgumentNullException.ThrowIfNull(importVariant);

        _logger.LogInformation($"Variant Update Sync running. SKU: {importVariant.SKU}, SyncUser: {syncUser}");

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = _contentService
            .GetPagedDescendants(umbracoRootContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .Where(x => !x.GetValue<bool>("ekmDisableSync")).ToList();

        var variant = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmProductVariant" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importVariant.Identifier);

        if (variant == null)
        {
            throw new ArgumentNullException(nameof(variant), $"Variant is null. Identifier: {importVariant.Identifier} SKU: {importVariant.SKU} ParentKey: {parentKey}");
        }

        SaveVariant(variant, importVariant, null, false, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Variant Update Sync finished VariantSku: {SKU} ProductId: {Id}", importVariant.SKU, variant.Id);
    }
    public void CategoryUpdateSync(ImportCategory importCategory, Guid? parentKey, int syncUser = -1)
    {
        ArgumentNullException.ThrowIfNull(importCategory);

        _logger.LogInformation($"Category Update Sync running. SKU: {importCategory.SKU}, SyncUser: {syncUser}");

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = _contentService
            .GetPagedDescendants(umbracoRootContent.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .Where(x => !x.GetValue<bool>("ekmDisableSync")).ToList();

        var category = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmCategory" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importCategory.Identifier);

        if (category == null)
        {
            throw new ArgumentNullException(nameof(category), $"Category is null. Identifier: {importCategory.Identifier} SKU: {importCategory.SKU} ParentKey: {parentKey}");
        }

        SaveCategory(category, importCategory, null, false, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Category Update Sync finished {SKU} CategoryId: {Id}", importCategory.SKU, category.Id);
    }
    public void SyncProductMedia(string identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        productContentType = _contentTypeService.Get("ekmProduct");

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoProducts = GetAllUmbracoProducts();

        var umbracoProduct = allUmbracoProducts.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifier);

        ArgumentNullException.ThrowIfNull(umbracoProduct);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        ImportMedia(umbracoProduct, medias, allUmbracoMedia, mediaType, mediaContentType, true, syncUser);
    }
    public void SyncVariantMedia(string identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        productVariantContentType = _contentTypeService.Get("ekmProductVariant");

        var allUmbracoVariants = GetAllUmbracoVariants();

        var umbracoVariant = allUmbracoVariants.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifier);

        ArgumentNullException.ThrowIfNull(umbracoVariant);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        ImportMedia(umbracoVariant, medias, allUmbracoMedia, mediaType, mediaContentType, true, syncUser);
    }

    private void IterateCategoryTree(List<ImportCategory>? importCategories, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, IContent? parentContent, int syncUser, bool delete = true)
    {

        if (parentContent == null)
        {
            return;
        }

        if (importCategories == null)
        {
            return;
        }

        if (delete)
        {
            // Delete Categories

            // Create a HashSet of identifiers from importCategory for efficient lookups
            var importCategoryIdentifiers = importCategories == null ? new HashSet<string>() : new HashSet<string>(importCategories.Select(x => x.Identifier));

            var targetedCategores = allUmbracoCategories.Where(x => x.Path.Contains(umbracoRootContent.Id.ToString(), StringComparison.InvariantCulture)).Where(x => x.Path.Split(',').Contains(umbracoRootContent.Id.ToString())).ToList();

            // Delete Category not present in the importCategoryIdentifiers
            for (int i = targetedCategores.Count - 1; i >= 0; i--)
            {
                var umbracoCategory = targetedCategores[i];

                var isSyncDisabled = umbracoCategory.HasProperty("ekmDisableSync") && umbracoCategory.GetValue<bool>("ekmDisableSync");

                if (isSyncDisabled)
                {
                    continue;
                }

                if (umbracoCategory.ParentId == parentContent.Id)
                {
                    var categoryIdentifier = umbracoCategory.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";
                    if (!importCategoryIdentifiers.Contains(categoryIdentifier))
                    {
                        _logger.LogInformation($"Delete category Id: {umbracoCategory.Id} Name: {umbracoCategory.Name} Identifier: {categoryIdentifier}");

                        _contentService.Delete(umbracoCategory);
                        categoriesDeleted++;
                    }
                }
            }
        }


        if (importCategories != null || importCategories?.Count > 0)
        {
            foreach (var importCategory in importCategories)
            {
                var umbracoChildrenContent = allUmbracoCategories.Where(x => x.ParentId == parentContent.Id).ToList();

                var content = GetOrCreateContent(categoryContentType, umbracoChildrenContent, importCategory.NodeName, importCategory.Identifier, parentContent, out bool create);

                if (content == null)
                {
                    continue;
                }

                if (create)
                {
                    allUmbracoCategories.Add(content);
                }

                var save = create;

                SaveCategory(content, importCategory, allUmbracoMedia, create, syncUser);

                IterateCategoryTree(importCategory.SubCategories, allUmbracoCategories, allUmbracoMedia, content, syncUser);
            }
        }

    }

    private void IterateProductTree(List<ImportProduct> importProducts, List<IContent> allEkomNodes, List<IContent> allUmbracoProducts, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, int syncUser, bool delete = true)
    {
        ArgumentNullException.ThrowIfNull(categoryContentType);
        ArgumentNullException.ThrowIfNull(productContentType);
        ArgumentNullException.ThrowIfNull(umbracoRootContent);
        ArgumentNullException.ThrowIfNull(catalogContentType);

        if (importProducts != null || importProducts?.Count > 0)
        {

            if (delete)
            {
                var targetedUmbracoProducts = allUmbracoProducts.Where(x => x.ParentId == umbracoRootContent.Id).ToList();

                // Create a HashSet of identifiers from importProducts for efficient lookups
                var importProductIdentifiers = new HashSet<string>(importProducts.Select(x => x.Identifier));

                // Delete Products not present in the importProductIdentifiers or that are duplicates
                for (int i = targetedUmbracoProducts.Count - 1; i >= 0; i--)
                {
                    var umbracoProduct = targetedUmbracoProducts[i];
                    var isSyncDisabled = umbracoProduct.HasProperty("ekmDisableSync") && umbracoProduct.GetValue<bool>("ekmDisableSync");

                    if (isSyncDisabled)
                    {
                        continue;
                    }

                    var productIdentifier = umbracoProduct.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";

                    // Check if the identifier is valid and not in the import list
                    if (!importProductIdentifiers.Contains(productIdentifier))
                    {
                        _logger.LogInformation($"Product deleted Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} ProductIdentifier: {productIdentifier}");
                        _contentService.Delete(umbracoProduct);
                        productDeleted++;
                        continue;
                    }

                    // If product has moved from primary category we want to delete it and recreate it in the new primary category
                    var parentCategory = allUmbracoCategories.FirstOrDefault(x => x.Id == umbracoProduct.ParentId);

                    if (parentCategory != null)
                    {
                        var categoryIdentifer = parentCategory.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";
                        var importProduct = importProducts.FirstOrDefault(x => x.Identifier == productIdentifier);

                        if (importProduct != null)
                        {
                            // If Primary category is not the same as the parent category identifer we want to delete the product
                            if (importProduct.Categories != null && importProduct.Categories.Any() && (importProduct.Categories.First() != categoryIdentifer))
                            {
                                _logger.LogInformation($"Product deleted, product moved. Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} ProductIdentifier: {productIdentifier}");
                                _contentService.Delete(umbracoProduct);
                                productDeleted++;
                            }

                        }

                    }

                }
            }

            foreach (var importProduct in importProducts)
            {
                if (importProduct.Categories.Count > 0)
                {
                    IContent? primaryCategoryContent = null;

                    foreach (var categoryIdentifier in importProduct.Categories)
                    {
                        var primaryCategory = allUmbracoCategories.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == categoryIdentifier);

                        if (primaryCategory != null)
                        {
                            primaryCategoryContent = primaryCategory;
                            break;
                        }

                    }

                    if (primaryCategoryContent != null)
                    {
                        var umbracoChildrenContent = allUmbracoProducts.Where(x => x.ParentId == primaryCategoryContent.Id).ToList();

                        var productContent = GetOrCreateContent(productContentType, umbracoChildrenContent, importProduct.NodeName, importProduct.Identifier, primaryCategoryContent, out bool create);

                        if (productContent == null)
                        {
                            continue;
                        }

                        var save = create;

                        SaveProduct(productContent, importProduct, allUmbracoCategories, allUmbracoMedia, create, syncUser);

                        IterateVariantGroups(importProduct.VariantGroups, productContent, allEkomNodes, allUmbracoMedia, syncUser);
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to save product {importProduct.SKU}, no primary category found. '{string.Join(",", importProduct.Categories)}'");
                    }
                }
                else
                {
                    _logger.LogWarning($"Failed to save product {importProduct.SKU}, no categories found.");
                }

            }
        }

    }

    private void IterateVariantGroups(List<ImportVariantGroup> importVariantGroups, IContent productContent, List<IContent> allEkomNodes, List<IMedia> allUmbracoMedia, int syncUser)
    {
        var umbracoVariantGroupChildrenContent = allEkomNodes.Where(x => x.ParentId == productContent.Id).ToList();

        // Delete Variant Groups

        // Create a HashSet of identifiers from importVariantGroup for efficient lookups
        var importVariantGroupsIdentifiers = new HashSet<string>(importVariantGroups.Select(x => x.Identifier));

        // Delete VariantGroup not present
        for (int i = umbracoVariantGroupChildrenContent.Count - 1; i >= 0; i--)
        {
            var umbracoVariantGroup = umbracoVariantGroupChildrenContent[i];

            var variantGroupIdentifier = umbracoVariantGroup.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";
            if (!importVariantGroupsIdentifiers.Contains(variantGroupIdentifier))
            {
                _logger.LogInformation($"Delete variant Group Id: {umbracoVariantGroup.Id} Name: {umbracoVariantGroup.Name} Identifier: {variantGroupIdentifier}");

                _contentService.Delete(umbracoVariantGroup);
                allEkomNodes.RemoveAt(i);
                umbracoVariantGroupChildrenContent.RemoveAt(i);
                variantGroupDeleted++;
            }

        }

        foreach (var importVariantGroup in importVariantGroups)
        {
            var variantGroupContent = GetOrCreateContent(productVariantGroupContentType, umbracoVariantGroupChildrenContent, importVariantGroup.NodeName, importVariantGroup.Identifier, productContent, out bool create);

            if (variantGroupContent == null)
            {
                continue;
            }

            var save = create;

            SaveVariantGroup(variantGroupContent, importVariantGroup, allUmbracoMedia, create, syncUser);

            IterateVariants(importVariantGroup.Variants, variantGroupContent, allEkomNodes, allUmbracoMedia, syncUser);
        }
    }

    private void IterateVariants(List<ImportVariant> importVariants, IContent variantGroupContent, List<IContent> allEkomNodes, List<IMedia> allUmbracoMedia, int syncUser, bool delete = true)
    {
        var umbracoVariantsChildrenContent = allEkomNodes.Where(x => x.ParentId == variantGroupContent.Id).ToList();

        if (delete)
        {
            // Delete Variants

            // Create a HashSet of identifiers from importVariantGroup for efficient lookups
            var importVariantsIdentifiers = new HashSet<string>(importVariants.Select(x => x.Identifier));

            // Delete VariantGroup not present
            for (int i = umbracoVariantsChildrenContent.Count - 1; i >= 0; i--)
            {
                var umbracoVariant = umbracoVariantsChildrenContent[i];

                var variantIdentifier = umbracoVariant.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";
                if (!importVariantsIdentifiers.Contains(variantIdentifier))
                {
                    _logger.LogInformation($"Delete variant Id: {umbracoVariant.Id} Name: {umbracoVariant.Name} Identifier: {variantIdentifier}");

                    _contentService.Delete(umbracoVariant);
                    allEkomNodes.RemoveAt(i);
                    umbracoVariantsChildrenContent.RemoveAt(i);
                    variantDeleted++;
                }

            }
        }


        foreach (var importVariant in importVariants)
        {
            var variantContent = GetOrCreateContent(productVariantContentType, umbracoVariantsChildrenContent, importVariant.NodeName, importVariant.Identifier, variantGroupContent, out bool create);

            if (variantContent == null)
            {
                continue;
            }

            var save = create;

            SaveVariant(variantContent, importVariant, allUmbracoMedia, create, syncUser);
        }
    }

    private void SaveCategory(IContent categoryContent, ImportCategory importCategory, List<IMedia> allUmbracoMedia, bool create, int syncUser)
    {
        try
        {
            OnCategorySaveStarting(this, new ImportCategoryEventArgs(categoryContent, importCategory, create)).GetAwaiter().GetResult();

            var saveImages = ImportMedia(categoryContent, importCategory.Images, allUmbracoMedia);

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

            categoryContent.SetValue(Configuration.ImportAliasIdentifier, importCategory.Identifier);

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

            SaveEvent(categoryContent, importCategory.SaveEvent, syncUser, create);

            categoriesSaved.Add(importCategory);

        }
        catch (Exception ex)
        {
            throw new Exception("Failed to save Category: " + importCategory.Identifier, ex);
        }

    }
    private void SaveProduct(IContent productContent, ImportProduct importProduct, List<IContent>? allUmbracoCategories, List<IMedia>? allUmbracoMedia, bool create, int syncUser)
    {
        try
        {
            OnProductSaveStarting(this, new ImportProductEventArgs(productContent, importProduct, create)).GetAwaiter().GetResult();

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

            var saveImages = false;
            var saveFiles = false;

            if (allUmbracoMedia is not null)
            {
                saveImages = ImportMedia(productContent, importProduct.Images, allUmbracoMedia);
                saveFiles = ImportMedia(productContent, importProduct.Files, allUmbracoMedia, ImportMediaTypes.File, ImportMediaContentTypes.files);
            }

            var compareValue = importProduct.Comparer ?? ComputeSha256Hash(importProduct, new string[] { "VariantGroups", "Images", "EventProperties", "Files" });

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

            productContent.SetValue(Configuration.ImportAliasIdentifier, importProduct.Identifier);

            if (importProduct.Price.Any())
            {
                foreach (var price in importProduct.Price)
                {
                    productContent.SetPrice(price.StoreAlias, price.Currency, price.Price);
                }
            }

            if (importProduct.Vat.HasValue)
            {
                productContent.SetValue("vat", importProduct.Vat);
            }

            if (importProduct.AdditionalProperties != null && importProduct.AdditionalProperties.Any())
            {
                foreach (var property in importProduct.AdditionalProperties)
                {
                    productContent.SetValue(property.Key, property.Value);
                }
            }

            if (importProduct.Categories.Count > 1 && allUmbracoCategories != null)
            {
                var umbracoCategories = allUmbracoCategories.Where(x => importProduct.Categories.Skip(1).Contains(x.GetValue<string>(Configuration.ImportAliasIdentifier)));

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

            SaveEvent(productContent, importProduct.SaveEvent, syncUser, create);

            productsSaved.Add(importProduct);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save Product: Sku: {importProduct.SKU} Message: {ex.Message}");
            throw new Exception($"Failed to save Product: Sku: {importProduct.SKU} Message: {ex.Message}", ex);
        }
    }
    private void SaveVariantGroup(IContent variantGroupContent, ImportVariantGroup importVariantGroup, List<IMedia> allUmbracoMedia, bool create, int syncUser)
    {
        var saveImages = ImportMedia(variantGroupContent, importVariantGroup.Images, allUmbracoMedia);

        var compareValue = importVariantGroup.Comparer ?? ComputeSha256Hash(importVariantGroup, new string[] { "Variants", "Images", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(variantGroupContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages)
        {
            return;
        }

        variantGroupContent.SetProperty("title", importVariantGroup.Title);

        variantGroupContent.SetValue(Configuration.ImportAliasIdentifier, importVariantGroup.Identifier);

        if (importVariantGroup.AdditionalProperties != null && importVariantGroup.AdditionalProperties.Any())
        {
            foreach (var property in importVariantGroup.AdditionalProperties)
            {
                variantGroupContent.SetValue(property.Key, property.Value);
            }
        }

        variantGroupContent.SetValue("comparer", compareValue);

        variantGroupContent.Name = importVariantGroup.NodeName;

        SaveEvent(variantGroupContent, importVariantGroup.SaveEvent, syncUser, create);

        variantGroupsSaved.Add(importVariantGroup);
    }
    private void SaveVariant(IContent variantContent, ImportVariant importVariant, List<IMedia>? allUmbracoMedia, bool create, int syncUser)
    {
        OnVariantSaveStarting(this, new ImportVariantEventArgs(variantContent, importVariant, create)).GetAwaiter().GetResult();

        // Always do stock update
        if (importVariant.Stock.Any())
        {
            foreach (var stock in importVariant.Stock)
            {
                var currentStock = _stock.GetStock(variantContent.Key);
                var newStock = stock.Stock >= 0 ? stock.Stock : 0;

                // Only update if we find change 
                if (newStock != currentStock)
                {
                    var stockUpdated = _stock.SetStockAsync(variantContent.Key, stock.StoreAlias, stock.Stock).Result;
                }
            }
        }

        var saveImages = false;
        var saveFiles = false;

        if (allUmbracoMedia != null)
        {
            saveImages = ImportMedia(variantContent, importVariant.Images, allUmbracoMedia);

            saveFiles = ImportMedia(variantContent, importVariant.Files, allUmbracoMedia, ImportMediaTypes.File, ImportMediaContentTypes.files);
        }

        var compareValue = importVariant.Comparer ?? ComputeSha256Hash(importVariant, new string[] { "Images", "EventProperties", "Files" });

        // If no changes are found and not creating then return,
        if (!HasContentChanges(variantContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages && !saveFiles)
        {
            return;
        }

        variantContent.SetProperty("title", importVariant.Title);

        if (!string.IsNullOrEmpty(importVariant.SKU))
        {
            variantContent.SetValue("sku", importVariant.SKU);
        }

        if (variantContent.HasProperty("description"))
        {
            variantContent.SetProperty("description", importVariant.Description);
        }

        variantContent.SetValue(Configuration.ImportAliasIdentifier, importVariant.Identifier);

        if (importVariant.Price.Any())
        {
            foreach (var price in importVariant.Price)
            {
                variantContent.SetPrice(price.StoreAlias, price.Currency, price.Price);
            }
        }
        if (importVariant.Vat.HasValue)
        {
            variantContent.SetValue("vat", importVariant.Vat);
        }

        if (importVariant.AdditionalProperties != null && importVariant.AdditionalProperties.Any())
        {
            foreach (var property in importVariant.AdditionalProperties)
            {
                variantContent.SetValue(property.Key, property.Value);
            }
        }

        variantContent.SetValue("comparer", compareValue);

        variantContent.Name = importVariant.NodeName;

        SaveEvent(variantContent, importVariant.SaveEvent, syncUser, create);

        variantsSaved.Add(importVariant);
    }
    private bool ImportMedia(IContent content, List<IImportMedia> importMedias, List<IMedia>? allUmbracoMedia, ImportMediaTypes mediaType = ImportMediaTypes.Image, ImportMediaContentTypes contentTypeAlias = ImportMediaContentTypes.images, bool saveContent = false, int syncUser = -1)
    {

        if (allUmbracoMedia == null)
        {
            return false;
        }

        var currentImages = (content.GetValue<string>(contentTypeAlias.ToString()) ?? "").TrimStart(',');

        var currentImagesUdi = new List<string>();

        if (currentImages.StartsWith("[", StringComparison.InvariantCultureIgnoreCase))
        {
            try
            {
                var mediaObjects = System.Text.Json.JsonSerializer.Deserialize<List<MediaObject>>(currentImages);

                if (mediaObjects != null && mediaObjects.Any())
                {
                    currentImagesUdi.AddRange(mediaObjects.Select(x => "umb://media/" + x.MediaKey.Replace("-", "")));
                    currentImages = string.Join(",", currentImagesUdi);
                }
            } catch
            {
                _logger.LogWarning($"Could not parse media json value on product {content.Id}. Value: {currentImages}");
                currentImagesUdi.Clear();
                currentImages = "";
            }
        }
        else
        {
            currentImagesUdi.AddRange(currentImages.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        var currentImagesCount = currentImagesUdi.Count;

        var sortedMedias = importMedias
            .Select((media, index) => new { media, index })
            .OrderBy(x => x.media.SortOrder.HasValue ? x.media.SortOrder.Value : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => x.media);

        foreach (var media in sortedMedias)
        {
            if (media is ImportMediaFromUdi importMedia)
            {
                if (media.Action == ImportMediaAction.Add)
                {
                    AddUdiIfNotExist(currentImagesUdi, importMedia.Udi);
                } else if (media.Action == ImportMediaAction.Delete)
                {
                    RemoveUdiIfExist(currentImagesUdi, importMedia.Udi);
                }
            }
            else if (media is ImportMediaFromExternalUrl externalUrlMedia)
            {
                var compareValue = externalUrlMedia.Comparer ?? ComputeSha256Hash(externalUrlMedia, new string[] { "Url", "FileName", "NodeName", "Date" });

                var umbMedia = allUmbracoMedia.FirstOrDefault(x =>
                    x.HasProperty("ekmIdentifier") && !string.IsNullOrEmpty(externalUrlMedia.Identifier)
                        ? x.GetValue<string>("ekmIdentifier") == externalUrlMedia.Identifier
                        : x.GetValue<string>("comparer") == compareValue);

                if (media.Action == ImportMediaAction.Add)
                {
                    if (umbMedia == null)
                    {
                        // Create
                        umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier);

                        if (umbMedia != null)
                        {
                            allUmbracoMedia.Add(umbMedia);
                            AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                        }

                    } else
                    {   // Update

                        // Remove media
                        RemoveUdiIfExist(currentImagesUdi, umbMedia.GetUdi().ToString());

                        umbMedia = allUmbracoMedia.FirstOrDefault(x => x.GetValue<string>("comparer") == compareValue);

                        // If the media is not found by comparer we need to create a new media
                        if (umbMedia == null)
                        {
                            umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier);

                            if (umbMedia != null)
                            {
                                allUmbracoMedia.Add(umbMedia);
                                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                            }

                        } else
                        {
                            // If comparer is the same probably the sort order has just changed so we just want to change the order.
                            _importMediaService.UpdateMediaSortOrder(umbMedia, externalUrlMedia);
                            AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                        }

                    } 

                } else if (media.Action == ImportMediaAction.Delete) {

                    if (umbMedia != null)
                    {
                        RemoveUdiIfExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                    }
                }

            }
            else if (media is ImportMediaFromBytes bytesMedia)
            {
                var compareValue = bytesMedia.Comparer ?? ComputeSha256Hash(bytesMedia, new string[] { "Bytes" });

                var umbMedia = allUmbracoMedia.FirstOrDefault(x => x.HasProperty("ekmIdentifier") && !string.IsNullOrEmpty(bytesMedia.Identifier) ? x.GetValue<string>("ekmIdentifier") == bytesMedia.Identifier : x.GetValue<string>("comparer") == compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBytes(bytesMedia, compareValue, mediaType, bytesMedia.Identifier);
                    allUmbracoMedia.Add(umbMedia);
                }

                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
            }
            else if (media is ImportMediaFromBase64 base64Media)
            {

                var compareValue = base64Media.Comparer ?? ComputeSha256Hash(base64Media, new string[] { "Base64" });

                var umbMedia = allUmbracoMedia.FirstOrDefault(x => x.HasProperty("ekmIdentifier") && !string.IsNullOrEmpty(base64Media.Identifier) ? x.GetValue<string>("ekmIdentifier") == base64Media.Identifier : x.GetValue<string>("comparer") == compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBase64(base64Media, compareValue, mediaType, base64Media.Identifier);
                    allUmbracoMedia.Add(umbMedia);
                }

                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
            }
        }

        if (currentImagesCount == 0 && currentImagesUdi.Count == 0)
        {
            return false;
        }

        var importedImages = SortImages(currentImagesUdi.DistinctBy(x => x).ToList(), allUmbracoMedia);

        if (currentImages != importedImages)
        {
            content.SetValue(contentTypeAlias.ToString(), importedImages);

            if (saveContent)
            {
                using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
                {
                    if (content.Published)
                    {
                        _contentService.SaveAndPublish(content, userId: syncUser);
                    } else
                    {
                        _contentService.Save(content, userId: syncUser);
                    }
                }
            }

            return true;
        }
        return false;
    }

    private string SortImages(List<string> imagesUdi, List<IMedia> allUmbracoMedia)
    {
        // Convert allUmbracoMedia to a dictionary for fast lookup
        var mediaDictionary = allUmbracoMedia.ToDictionary(
            media => "umb://media/" + media.Key.ToString().Replace("-", "", StringComparison.InvariantCultureIgnoreCase)
        );

        var images = new List<(IMedia Media, int SortOrder)>();

        // Populate images list with media and sort order
        foreach (var imageUdi in imagesUdi)
        {
            if (mediaDictionary.TryGetValue(imageUdi, out var imageNode))
            {
                int sortOrder = imageNode.HasProperty("ekmSortOrder") &&
                                int.TryParse(imageNode.GetValue<string>("ekmSortOrder"), out var parsedOrder)
                                ? parsedOrder
                                : int.MaxValue;

                images.Add((imageNode, sortOrder));
            }
        }

        // Sort based on sortOrder, then join keys as a string
        var sortedImageUris = images
            .OrderBy(image => image.SortOrder)
            .Select(image => "umb://media/" + image.Media.Key.ToString().Replace("-", "", StringComparison.InvariantCultureIgnoreCase));

        return string.Join(",", sortedImageUris);
    }

    private void AddUdiIfNotExist(List<string> imagesUdi, string udi)
    {
        if (!imagesUdi.Contains(udi))
        {
            imagesUdi.Add(udi);
        }
    }
    private void RemoveUdiIfExist(List<string> imagesUdi, string udi)
    {
        if (imagesUdi.Contains(udi))
        {
            imagesUdi.Remove(udi);
        }
    }

    private void SaveEvent(IContent content, ImportSaveEntEnum saveEvent, int syncUser, bool create)
    {
        using (var contextReference = _umbracoContextFactory.EnsureUmbracoContext())
        {
            if (saveEvent == ImportSaveEntEnum.SavePublish || create)
            {
                _contentService.SaveAndPublish(content, userId: syncUser);
            }
            else if (saveEvent == ImportSaveEntEnum.Unpublish && create)
            {
                _contentService.Save(content, userId: syncUser);
            }
            else if (saveEvent == ImportSaveEntEnum.Unpublish)
            {
                _contentService.Save(content, userId: syncUser);
                _contentService.Unpublish(content, userId: syncUser);
            }
            else
            {
                _contentService.Save(content, userId: syncUser);
            }
        }
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
        productsSaved = new List<ImportProduct>();
        variantsSaved = new List<ImportVariant>();
        variantGroupsSaved = new List<ImportVariantGroup>();
        categoriesSaved = new List<ImportCategory>();

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
    /// <returns>The found or newly created content item.</returns>
    private IContent? GetOrCreateContent(IContentType? contenType, List<IContent> umbracoChildrenContent, string nodeName, string identifer, IContent parentContent, out bool create)
    {
        ArgumentNullException.ThrowIfNull(contenType);
        ArgumentNullException.ThrowIfNull(nodeName);
        ArgumentNullException.ThrowIfNull(identifer);

        create = false;
        var content = umbracoChildrenContent.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifer);

        if (content != null && content.HasProperty("ekmDisableSync") && content.GetValue<bool>("ekmDisableSync"))
        {
            return null;
        }

        if (content == null)
        {
            // Note: Assuming 'Content' is a constructor for an object that implements IContent
            // and 'categoryContentType' is defined elsewhere in your class.
            content = new Umbraco.Cms.Core.Models.Content(nodeName, parentContent.Id, contenType);
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
    private List<IContent> GetAllUmbracoVariants()
    {
        ArgumentNullException.ThrowIfNull(productVariantContentType);

        var categories = _contentService
            .GetPagedOfType(productVariantContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .ToList();

        return categories;
    }
    private List<IContent> GetAllUmbracoCategories()
    {
        ArgumentNullException.ThrowIfNull(categoryContentType);

        var categories = _contentService
            .GetPagedOfType(categoryContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .ToList();

        return categories;
    }

    private List<IContent> GetAllUmbracoProducts()
    {
        ArgumentNullException.ThrowIfNull(productContentType);

        var categories = _contentService
            .GetPagedOfType(productContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .ToList();

        return categories;
    }
    private List<IContent> GetAllEkomNodes()
    {
        ArgumentNullException.ThrowIfNull(catalogContentType);

        var catalogNode = _contentService
                .GetPagedOfType(catalogContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
                .Where(x => !x.Trashed)).FirstOrDefault();

        ArgumentNullException.ThrowIfNull(catalogNode);

        var categories = _contentService
            .GetPagedDescendants(catalogNode.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .ToList();

        return categories;
    }

    public class MediaObject
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }
        [JsonPropertyName("mediaKey")]
        public string MediaKey { get; set; }
    }

    public class BackgroundScope : IDisposable
    {
        private readonly IServerMessenger _serverMessenger;

        public BackgroundScope(IServerMessenger serverMessenger)
        {
            _serverMessenger = serverMessenger;
        }

        public void Dispose()
        {
            if (_serverMessenger is BatchedDatabaseServerMessenger batchedDatabaseServerMessenger)
            {
                batchedDatabaseServerMessenger.SendMessages();
            }
        }
    }
}
