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
        _logger.LogInformation($"Full Sync running. ParentKey: {(parentKey.HasValue ? parentKey.Value.ToString() : "None")}, SyncUser: {syncUser}, Categories: {(data.Categories != null ?  (data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()) : 0)} Products: {data.Products.Count}");

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
            using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

            GetInitialData(parentKey);

            var allEkomNodes = GetAllEkomNodes();

            ArgumentNullException.ThrowIfNull(umbracoRootContent);
            var allUmbracoCategories = allEkomNodes
                .Where(x => x.ContentType.Alias == "ekmCategory").ToList();

            var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct")
                .ToList();

            var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

            var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);
            var mediaIndex = new ImportMediaIndex(allUmbracoMedia);

            var recycleBinNode = data.RecycleBinKey.HasValue ? _contentService.GetById(data.RecycleBinKey.Value) : null;
            var productProcessNode = data.ProductProcessKey.HasValue ? _contentService.GetById(data.ProductProcessKey.Value) : null;

            var stopwatch = Stopwatch.StartNew();

            IterateCategoryTree(data.Categories, GetAllCategories(data), allUmbracoCategories, allUmbracoMedia, mediaIndex, umbracoRootContent, syncUser);

            _logger.LogInformation("IterateCategoryTree took {Duration} seconds", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"));

            stopwatch.Stop();

            stopwatch.Restart();

            IterateProductTree(data.Products, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, mediaIndex, syncUser, true, recycleBinNode, productProcessNode);

            _logger.LogInformation("IterateProductTree took {Duration} seconds", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"));

            stopwatch.Stop();

            OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.FullSync)).GetAwaiter().GetResult();

            stopwatchTotal.Stop();

            var groupedFailedProducts = productsSaved
                .Where(x => x.Exception != null)
                .GroupBy(x => x.Exception.Message);

            foreach (var group in groupedFailedProducts)
            {
                var exceptionMessage = group.Key;
                var productSKUs = string.Join(", ", group.Select(x => x.SKU));

                _logger.LogError(
                    "Failed to save {count} Products with the same error: {exceptionMessage}. SKUs: {productSKUs}",
                    group.Count(),
                    exceptionMessage,
                    productSKUs
                );
            }

            _logger.LogInformation(
                "Full Sync took {Duration} seconds. Categories Saved: {categoriesCount} Products Saved: {productsCount} Products With Error: {productsErrorCount} Variants Saved: {variantsCount} VariantsGroups Saved: {variantGroupsCount} Categories Deleted: {categoriesDeleted} Products Deleted: {productDeleted} Variants Deleted: {variantDeleted} VariantsGroups Deleted: {variantGroupDeleted}",
                (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"), categoriesSaved.Count, productsSaved.Where(x => x.Exception == null).Count(), productsSaved.Where(x => x.Exception != null).Count(), variantsSaved.Count, variantGroupsSaved.Count, categoriesDeleted, productDeleted, variantDeleted, variantGroupDeleted);

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Full Sync failed");
            throw;
        }
        finally
        {
            lock (_syncLock)
            {
                _isFullSyncRunning = false;
            }
        }
    }

    public void MoveSync(ImportData data, Guid? parentKey = null, int syncUser = -1)
    {
        _logger.LogInformation($"Move Sync running. ParentKey: {(parentKey.HasValue ? parentKey.Value.ToString() : "None")}, SyncUser: {syncUser}, Categories: {(data.Categories != null ? (data.Categories.Count + data.Categories.SelectMany(x => x.SubCategories).Count()) : 0)} Products: {data.Products.Count}");

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
            using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

            GetInitialData(parentKey);

            var allEkomNodes = GetAllEkomNodes();

            ArgumentNullException.ThrowIfNull(umbracoRootContent);

            var allUmbracoCategories = allEkomNodes
                .Where(x => x.ContentType.Alias == "ekmCategory").ToList();


            var stopwatch = Stopwatch.StartNew();

            MoveCategoryTree(data.Categories, GetAllCategories(data), allUmbracoCategories, umbracoRootContent, syncUser);

            _logger.LogInformation("Move Category Tree took {Duration} seconds", (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"));

            stopwatch.Stop();
            stopwatchTotal.Stop();

            var groupedFailedProducts = productsSaved
                .Where(x => x.Exception != null)
                .GroupBy(x => x.Exception.Message);


            _logger.LogInformation(
                "Move Sync took {Duration} seconds. Categories Saved: {categoriesCount} Products Saved: {productsCount} Products With Error: {productsErrorCount} Variants Saved: {variantsCount} VariantsGroups Saved: {variantGroupsCount} Categories Deleted: {categoriesDeleted} Products Deleted: {productDeleted} Variants Deleted: {variantDeleted} VariantsGroups Deleted: {variantGroupDeleted}",
                (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"), categoriesSaved.Count, productsSaved.Where(x => x.Exception == null).Count(), productsSaved.Where(x => x.Exception != null).Count(), variantsSaved.Count, variantGroupsSaved.Count, categoriesDeleted, productDeleted, variantDeleted, variantGroupDeleted);

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Move Sync failed");
            throw;
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
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory").ToList();
        var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(data.MediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);
        var mediaIndex = new ImportMediaIndex(allUmbracoMedia);

        var recycleBinNode = data.RecycleBinKey.HasValue ? _contentService.GetById(data.RecycleBinKey.Value) : null;
        var productProcessNode = data.ProductProcessKey.HasValue ? _contentService.GetById(data.ProductProcessKey.Value) : null;

        IterateCategoryTree(data.Categories, new List<ImportCategory>(), allUmbracoCategories, allUmbracoMedia, mediaIndex, umbracoRootContent, syncUser, false);

        if (data.Products != null && data.Products.Any())
        {
            IterateProductTree(data.Products, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, mediaIndex, syncUser, true, recycleBinNode, productProcessNode);
        }

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.CategorySync)).GetAwaiter().GetResult();

        stopwatch.Stop();

        _logger.LogInformation(
            "Category Sync took {Duration} seconds. Parent {parentKey} Categories Saved: {categoriesCount} Products Saved: {productsCount} Products With Error: {productsErrorCount} Variants Saved: {variantsCount} VariantsGroups Saved: {variantGroupsCount} Categories Deleted: {categoriesDeleted} Products Deleted: {productDeleted} Variants Deleted: {variantDeleted} VariantsGroups Deleted: {variantGroupDeleted}",
            (stopwatch.ElapsedMilliseconds / 1000.0).ToString("F2"), parentKey, categoriesSaved.Count, productsSaved.Where(x => x.Exception == null).Count(), productsSaved.Where(x => x.Exception != null).Count(), variantsSaved.Count, variantGroupsSaved.Count, categoriesDeleted, productDeleted, variantDeleted, variantGroupDeleted);
    }

    public void ProductSync(ImportProduct importProduct, Guid? parentKey, Guid mediaRootKey, int syncUser = -1, bool forceUpdate = false)
    {
        _logger.LogInformation($"Product Sync running. SKU: {importProduct.SKU}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory").ToList();
        var allUmbracoProducts = allEkomNodes.Where(x => x.ContentType.Alias == "ekmProduct").ToList();

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);
        var mediaIndex = new ImportMediaIndex(allUmbracoMedia);

        IterateProductTree(new List<ImportProduct> { importProduct }, allEkomNodes, allUmbracoProducts, allUmbracoCategories, allUmbracoMedia, mediaIndex, syncUser, false, forceUpdate: forceUpdate);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.ProductSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Sync finished ProductKey: {SKU}", importProduct.SKU);
    }
    public void VariantGroupSync(ImportVariantGroup importVariantGroup, Guid parentKey, Guid mediaRootKey, int syncUser = -1)
    {
        _logger.LogInformation($"Variant Group Sync running. SKU: {importVariantGroup.Identifier}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);
        var mediaIndex = new ImportMediaIndex(allUmbracoMedia);

        var allEkomNodes = GetAllEkomNodes();

        IterateVariantGroups(new List<ImportVariantGroup> { importVariantGroup }, umbracoRootContent, allEkomNodes, allUmbracoMedia, mediaIndex, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantGroupSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Variant Group finished: {SKU}", importVariantGroup.Identifier);
    }
    public void VariantSync(ImportVariant importVariant, Guid parentKey, Guid mediaRootKey, int syncUser = -1)
    {
        _logger.LogInformation($"Variant Sync running. SKU: {importVariant.SKU}, SyncUser: {syncUser}");

        using var backgroundScope = new BackgroundScope(_serverMessenger);
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);
        var mediaIndex = new ImportMediaIndex(allUmbracoMedia);

        var allEkomNodes = GetAllEkomNodes();

        IterateVariants(new List<ImportVariant> { importVariant }, umbracoRootContent, allEkomNodes, allUmbracoMedia, mediaIndex, syncUser, false);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Variant finished: {SKU}", importVariant.SKU);
    }
    public void ProductUpdateSync(ImportProduct importProduct, Guid? parentKey, Guid mediaRootKey, int syncUser = -1, bool forceUpdate = false)
    {
        _logger.LogInformation($"Product Update Sync running. SKU: {importProduct.SKU}, SyncUser: {syncUser}");

        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        var allEkomNodes = GetAllEkomNodes();

        var allUmbracoCategories = allEkomNodes.Where(x => x.ContentType.Alias == "ekmCategory").ToList();

        var product = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmProduct" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importProduct.Identifier);

        if (product == null)
        {
            throw new ArgumentNullException(nameof(product), $"Product is null. Identifier: {importProduct.Identifier} SKU: {importProduct.SKU} ParentKey: {parentKey}");
        }

        SaveProduct(product, importProduct, allUmbracoCategories, null, null, false, syncUser, forceUpdate: forceUpdate);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.ProductUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Product Update Sync finished ProductKey: {SKU} ProductId: {Id}", importProduct.SKU, product.Id);
    }
    public void VariantUpdateSync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1)
    {
        ArgumentNullException.ThrowIfNull(importVariant);

        _logger.LogInformation($"Variant Update Sync running. SKU: {importVariant.SKU}, SyncUser: {syncUser}");

        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var variant = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmProductVariant" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importVariant.Identifier);

        if (variant == null)
        {
            throw new ArgumentNullException(nameof(variant), $"Variant is null. Identifier: {importVariant.Identifier} SKU: {importVariant.SKU} ParentKey: {parentKey}");
        }

        SaveVariant(variant, importVariant, null, null, false, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Variant Update Sync finished VariantSku: {SKU} ProductId: {Id}", importVariant.SKU, variant.Id);
    }
    public void CategoryUpdateSync(ImportCategory importCategory, Guid? parentKey, int syncUser = -1)
    {
        ArgumentNullException.ThrowIfNull(importCategory);

        _logger.LogInformation($"Category Update Sync running. SKU: {importCategory.SKU}, SyncUser: {syncUser}");

        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        GetInitialData(parentKey);

        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var allEkomNodes = GetAllEkomNodes();

        var category = allEkomNodes.FirstOrDefault(x => x.ContentType.Alias == "ekmCategory" && x.GetValue<string>(Configuration.ImportAliasIdentifier) == importCategory.Identifier);

        if (category == null)
        {
            throw new ArgumentNullException(nameof(category), $"Category is null. Identifier: {importCategory.Identifier} SKU: {importCategory.SKU} ParentKey: {parentKey}");
        }

        SaveCategory(category, importCategory, null, null, false, syncUser);

        OnSyncFinished(this, new ImportSyncFinishedEventArgs(categoriesSaved, productsSaved, variantsSaved, variantGroupsSaved, ImportSyncType.VariantUpdateSync)).GetAwaiter().GetResult();

        _logger.LogInformation("Category Update Sync finished {SKU} CategoryId: {Id}", importCategory.SKU, category.Id);
    }
    public void SyncProductMedia(string identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        productContentType = _contentTypeService.Get("ekmProduct");

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoProducts = GetAllUmbracoProducts();

        var umbracoProduct = allUmbracoProducts.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifier);

        ArgumentNullException.ThrowIfNull(umbracoProduct);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        ImportSingleMedia(umbracoProduct, medias, allUmbracoMedia, new ImportMediaIndex(allUmbracoMedia), mediaType, mediaContentType, true, syncUser);
    }
    public void SyncVariantMedia(string identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();

        productVariantContentType = _contentTypeService.Get("ekmProductVariant");

        var allUmbracoVariants = GetAllUmbracoVariants();

        var umbracoVariant = allUmbracoVariants.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifier);

        ArgumentNullException.ThrowIfNull(umbracoVariant);

        var rootUmbracoMediafolder = _importMediaService.GetRootMedia(mediaRootKey);

        var allUmbracoMedia = _importMediaService.GetUmbracoMediaFiles(rootUmbracoMediafolder);

        ImportSingleMedia(umbracoVariant, medias, allUmbracoMedia, new ImportMediaIndex(allUmbracoMedia), mediaType, mediaContentType, true, syncUser);
    }
    private void MoveCategoryTree(List<ImportCategory>? importCategories, List<ImportCategory>? allImportCategories, List<IContent> allUmbracoCategories, IContent? parentContent, int syncUser)
    {

        if (parentContent == null || importCategories == null || allImportCategories == null)
        {
            return;
        }

        foreach (var importCategory in importCategories)
        {
            var content = GetOrCreateContent(categoryContentType, allUmbracoCategories, importCategory.NodeName, importCategory.Identifier, parentContent, syncUser, out bool create);

            if (content == null)
            {
                continue;
            }

            if (create)
            {
                allUmbracoCategories.Add(content);
            }
            

            var newParent = string.IsNullOrEmpty(importCategory.ParentIdentifier) ? umbracoRootContent : allUmbracoCategories.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == importCategory.ParentIdentifier);

            if (newParent != null && newParent.Id != content.ParentId)
            {
                _logger.LogInformation($"Category moved. Id: {content.Id} Name: {content.Name} Old Parent: {content.ParentId} New Parent: {newParent.Id} ParentIdentifier: {importCategory.ParentIdentifier}");


                try
                {
                    _contentService.Move(content, newParent.Id, syncUser);

                } catch(Exception ex)
                {
                    _logger.LogWarning($"Could not move Category  {content.Id} Name: {content.Name} Old Parent: {content.ParentId} New Parent: {newParent.Id}");
                }

                
            }

            MoveCategoryTree(importCategory.SubCategories, allImportCategories, allUmbracoCategories, content, syncUser);
        }
    }
    private void IterateCategoryTree(List<ImportCategory>? importCategories, List<ImportCategory>? allImportCategories, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, ImportMediaIndex mediaIndex, IContent? parentContent, int syncUser, bool delete = true)
    {

        if (parentContent == null || importCategories == null || allImportCategories == null)
        {
            return;
        }

        if (delete)
        {
            // Delete Categories

            // Create a HashSet of identifiers from importCategory for efficient lookups
            var importCategoryIdentifiers = new HashSet<string>(importCategories.Select(x => x.Identifier));

            // Delete Category not present in the importCategoryIdentifiers
            for (int i = allUmbracoCategories.Count - 1; i >= 0; i--)
            {
                var umbracoCategory = allUmbracoCategories[i];
                var isSyncDisabled = umbracoCategory.HasProperty("ekmDisableSync") && umbracoCategory.GetValue<bool>("ekmDisableSync");

                if (umbracoCategory.ParentId != parentContent.Id)
                {
                    continue;
                }

                if (isSyncDisabled)
                {
                    continue;
                }


                var categoryIdentifier = umbracoCategory.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";

                // Check if the category exists in the imported categories
                var importCategory = allImportCategories.FirstOrDefault(x => x.Identifier == categoryIdentifier);

                if (importCategory == null)
                {
                    // Category not found in import, so it should be deleted
                    _logger.LogInformation($"Delete category Id: {umbracoCategory.Id} Name: {umbracoCategory.Name} Identifier: {categoryIdentifier}");

                    _contentService.Delete(umbracoCategory, syncUser);
                    
                    allUmbracoCategories.Remove(umbracoCategory);
                    categoriesDeleted++;
                }

            }

        }

        foreach (var importCategory in importCategories)
        {
            var content = GetOrCreateContent(categoryContentType, allUmbracoCategories, importCategory.NodeName, importCategory.Identifier, parentContent, syncUser, out bool create);

            if (content == null)
            {
                continue;
            }

            if (create)
            {
                allUmbracoCategories.Add(content);
            }

            var newParent = string.IsNullOrEmpty(importCategory.ParentIdentifier) ? umbracoRootContent : allUmbracoCategories.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == importCategory.ParentIdentifier);

            if (newParent != null && newParent.Id != content.ParentId)
            {
                _logger.LogInformation($"Category moved. Id: {content.Id} Name: {content.Name} Old Parent: {content.ParentId} New Parent: {newParent.Id} ParentIdentifier: {importCategory.ParentIdentifier}");

                _contentService.Move(content, newParent.Id, syncUser);
                
            }

            var save = create;

            SaveCategory(content, importCategory, allUmbracoMedia, mediaIndex, create, syncUser);


            IterateCategoryTree(importCategory.SubCategories, allImportCategories, allUmbracoCategories, allUmbracoMedia, mediaIndex, content, syncUser, delete: delete);
        }
    }

    private void IterateProductTree(List<ImportProduct> importProducts, List<IContent> allEkomNodes, List<IContent> allUmbracoProducts, List<IContent> allUmbracoCategories, List<IMedia> allUmbracoMedia, ImportMediaIndex mediaIndex, int syncUser, bool delete = true, IContent? recycleBinNode = null, IContent? productProcessNode = null, bool forceUpdate = false)
    {
        ArgumentNullException.ThrowIfNull(categoryContentType);
        ArgumentNullException.ThrowIfNull(productContentType);
        ArgumentNullException.ThrowIfNull(umbracoRootContent);
        ArgumentNullException.ThrowIfNull(catalogContentType);

        _logger.LogInformation($"Iterating products. Import Count: {(importProducts != null ? importProducts.Count : 0)} Umbraco Product Count: {allUmbracoProducts.Count} Umbraco Categories Count: {allUmbracoCategories.Count} Delete: {delete}");

        if (importProducts != null && importProducts.Count > 0)
        {
            var umbracoCategoriesById = allUmbracoCategories.ToDictionary(x => x.Id);
            var umbracoCategoriesByIdentifier = BuildContentByIdentifier(allUmbracoCategories);
            var umbracoProductsByParentId = BuildChildrenByParentId(allUmbracoProducts);
            var ekomNodesByParentId = BuildChildrenByParentId(allEkomNodes);

            if (delete)
            {
                if (importProducts.Sum(x => x.Categories.Count) <= 0)
                    throw new ArgumentException("No products connected to categories in importProducts, sync stopped");

                var importProductIdentifiers = importProducts.Select(x => x.Identifier).ToHashSet();

                var importProductsById = new Dictionary<string, ImportProduct>();

                foreach (var product in importProducts)
                {
                    if (!importProductsById.TryAdd(product.Identifier, product))
                    {
                        _logger.LogWarning("Duplicate identifier '{Identifier}' found in importProducts, skipping duplicate", product.Identifier);
                    }
                }

                for (int i = allUmbracoProducts.Count - 1; i >= 0; i--)
                {
                    var umbracoProduct = allUmbracoProducts[i];

                    var productIdentifier = umbracoProduct.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";

                    if (umbracoProduct.HasProperty("ekmDisableSync") && umbracoProduct.GetValue<bool>("ekmDisableSync"))
                        continue;

                    // Not in import? Delete.
                    if (!importProductIdentifiers.Contains(productIdentifier))
                    {
                        if (recycleBinNode != null && recycleBinNode.Id == umbracoProduct.ParentId)
                        {
                            continue;
                        }

                        _logger.LogInformation($"Product deleted Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} ProductIdentifier: {productIdentifier}");

                        if (recycleBinNode != null)
                        {
                            _contentService.Unpublish(umbracoProduct, userId: syncUser);
                            _contentService.Move(umbracoProduct, recycleBinNode.Id, syncUser);
                        }
                        else
                        {
                            _contentService.Delete(umbracoProduct, syncUser);
                        }
     
                        productDeleted++;
                        continue;
                    }

                    // Check if product moved (also handles restore from recycle bin)
                    if (importProductsById.TryGetValue(productIdentifier, out var importProduct))
                    {
                        var newCategoryIdentifier = importProduct.Categories?.FirstOrDefault();

                        if (string.IsNullOrWhiteSpace(newCategoryIdentifier))
                            continue;

                        var isInRecycleBin = recycleBinNode != null && umbracoProduct.ParentId == recycleBinNode.Id;
                        var recycleBinIdentifier = recycleBinNode?.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";

                        if (newCategoryIdentifier.InvariantEquals(recycleBinIdentifier))
                        {
                            continue;
                        }

                        // Only read current category identifier if parent is actually a category (not recycle bin)
                        var currentCategoryIdentifier = "";
                        if (!isInRecycleBin && umbracoCategoriesById.TryGetValue(umbracoProduct.ParentId, out var parentCategory))
                        {
                            currentCategoryIdentifier = parentCategory.GetValue<string>(Configuration.ImportAliasIdentifier) ?? "";
                        }

                        // If in recycle bin, always allow "move" (restore). Otherwise only when identifier changed.
                        var needsMove = isInRecycleBin || newCategoryIdentifier != currentCategoryIdentifier;
                        if (!needsMove)
                            continue;

                        if (umbracoCategoriesByIdentifier.TryGetValue(newCategoryIdentifier, out var newCategory))
                        {
                            var oldParentId = umbracoProduct.ParentId;

                            _logger.LogInformation(
                                $"Product {(isInRecycleBin ? "restored" : "moved")}. " +
                                $"Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} " +
                                $"ProductIdentifier: {productIdentifier} Current Parent Category Identifier: {currentCategoryIdentifier} " +
                                $"New Parent Category Identifier: {newCategoryIdentifier}");

                            _contentService.Move(umbracoProduct, newCategory.Id, syncUser);
                            MoveIndexedContent(umbracoProductsByParentId, umbracoProduct, oldParentId, newCategory.Id);
                            MoveIndexedContent(ekomNodesByParentId, umbracoProduct, oldParentId, newCategory.Id);

                            if (isInRecycleBin)
                                _contentService.SaveAndPublish(umbracoProduct, userId: syncUser);
                        }
                        else
                        {
                            if (recycleBinNode != null)
                            {
                                _logger.LogInformation(
                                    $"Product deleted and moved to recycle bin. Product moved, category does not exist yet. " +
                                    $"Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} " +
                                    $"ProductIdentifier: {productIdentifier} Current Parent Category Identifier: {currentCategoryIdentifier} " +
                                    $"New Parent Category Identifier: {newCategoryIdentifier}");

                                _contentService.Unpublish(umbracoProduct, userId: syncUser);
                                var oldParentId = umbracoProduct.ParentId;
                                _contentService.Move(umbracoProduct, recycleBinNode.Id, syncUser);
                                MoveIndexedContent(umbracoProductsByParentId, umbracoProduct, oldParentId, recycleBinNode.Id);
                                MoveIndexedContent(ekomNodesByParentId, umbracoProduct, oldParentId, recycleBinNode.Id);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    $"Product deleted. Product moved, category does not exist yet. " +
                                    $"Id: {umbracoProduct.Id} Name: {umbracoProduct.Name} Parent: {umbracoProduct.ParentId} " +
                                    $"ProductIdentifier: {productIdentifier} Current Parent Category Identifier: {currentCategoryIdentifier} " +
                                    $"New Parent Category Identifier: {newCategoryIdentifier}");

                                _contentService.Delete(umbracoProduct, syncUser);
                            }

                            productDeleted++;
                        }
                    }

                }
            }

            _logger.LogInformation($"Iterating {importProducts.Count} products");

            foreach (var importProduct in importProducts)
            {
                if (importProduct.Categories.Count > 0)
                {
                    IContent? primaryCategoryContent = null;

                    foreach (var categoryIdentifier in importProduct.Categories)
                    {
                        if (umbracoCategoriesByIdentifier.TryGetValue(categoryIdentifier, out var primaryCategory))
                        {
                            primaryCategoryContent = primaryCategory;
                            break;
                        }

                    }

                    if (primaryCategoryContent != null)
                    {
                        var umbracoChildrenContent = GetIndexedChildren(umbracoProductsByParentId, primaryCategoryContent.Id);

                        var productContent = GetOrCreateContent(productContentType, umbracoChildrenContent, importProduct.NodeName, importProduct.Identifier, primaryCategoryContent, syncUser, out bool create);

                        if (productContent == null)
                        {
                            continue;
                        }

                        if (create)
                        {
                            allUmbracoProducts.Add(productContent);
                            umbracoChildrenContent.Add(productContent);
                            allEkomNodes.Add(productContent);
                            GetIndexedChildren(ekomNodesByParentId, primaryCategoryContent.Id).Add(productContent);
                        }

                        var save = create;

                        SaveProduct(productContent, importProduct, allUmbracoCategories, allUmbracoMedia, mediaIndex, create, syncUser, recycleBinNode, productProcessNode, forceUpdate);

                        IterateVariantGroups(importProduct.VariantGroups, productContent, allEkomNodes, allUmbracoMedia, mediaIndex, syncUser, ekomNodesByParentId, forceUpdate: forceUpdate);
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

    private void IterateVariantGroups(List<ImportVariantGroup> importVariantGroups, IContent productContent, List<IContent> allEkomNodes, List<IMedia> allUmbracoMedia, ImportMediaIndex mediaIndex, int syncUser, Dictionary<int, List<IContent>>? ekomNodesByParentId = null, bool forceUpdate = false)
    {
        ekomNodesByParentId ??= BuildChildrenByParentId(allEkomNodes);
        var umbracoVariantGroupChildrenContent = GetIndexedChildren(ekomNodesByParentId, productContent.Id);

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
                _logger.LogInformation($"Delete variant Group Id: {umbracoVariantGroup.Id} Name: {umbracoVariantGroup.Name} Identifier: {variantGroupIdentifier} Product Id: {productContent.Id} Product SKU: {productContent.GetValue<string>("sku")}");

                _contentService.Delete(umbracoVariantGroup);
                
                allEkomNodes.Remove(umbracoVariantGroup);
                umbracoVariantGroupChildrenContent.RemoveAt(i);
                variantGroupDeleted++;
            }

        }

        foreach (var importVariantGroup in importVariantGroups)
        {
            var variantGroupContent = GetOrCreateContent(productVariantGroupContentType, umbracoVariantGroupChildrenContent, importVariantGroup.NodeName, importVariantGroup.Identifier, productContent, syncUser, out bool create);

            if (variantGroupContent == null)
            {
                continue;
            }

            var save = create;

            if (create)
            {
                allEkomNodes.Add(variantGroupContent);
                umbracoVariantGroupChildrenContent.Add(variantGroupContent);
            }

            SaveVariantGroup(variantGroupContent, importVariantGroup, allUmbracoMedia, mediaIndex, create, syncUser, forceUpdate: forceUpdate);

            IterateVariants(importVariantGroup.Variants, variantGroupContent, allEkomNodes, allUmbracoMedia, mediaIndex, syncUser, ekomNodesByParentId: ekomNodesByParentId, forceUpdate: forceUpdate);
        }
    }

    private void IterateVariants(List<ImportVariant> importVariants, IContent variantGroupContent, List<IContent> allEkomNodes, List<IMedia> allUmbracoMedia, ImportMediaIndex mediaIndex, int syncUser, bool delete = true, Dictionary<int, List<IContent>>? ekomNodesByParentId = null, bool forceUpdate = false)
    {
        if (variantGroupContent == null || variantGroupContent.Id == 0)
        {
            _logger.LogError("Could not iterate variants. Variant Groups null or id 0. Name: {Name} Id: {Id} Path: {Path}", variantGroupContent?.Name ?? "", variantGroupContent?.Id, variantGroupContent?.Path ?? "");
            return;
        }

        ekomNodesByParentId ??= BuildChildrenByParentId(allEkomNodes);
        var umbracoVariantsChildrenContent = GetIndexedChildren(ekomNodesByParentId, variantGroupContent.Id);

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
  
                    allEkomNodes.Remove(umbracoVariant);
                    umbracoVariantsChildrenContent.RemoveAt(i);
                    variantDeleted++;
                }

            }
        }


        foreach (var importVariant in importVariants)
        {
            var variantContent = GetOrCreateContent(productVariantContentType, umbracoVariantsChildrenContent, importVariant.NodeName, importVariant.Identifier, variantGroupContent, syncUser, out bool create);

            if (variantContent == null)
            {
                continue;
            }

            var save = create;

            if (create)
            {
                allEkomNodes.Add(variantContent);
                umbracoVariantsChildrenContent.Add(variantContent);
            }

            SaveVariant(variantContent, importVariant, allUmbracoMedia, mediaIndex, create, syncUser, forceUpdate: forceUpdate);
        }
    }

    private void SaveCategory(IContent categoryContent, ImportCategory importCategory, List<IMedia>? allUmbracoMedia, ImportMediaIndex? mediaIndex, bool create, int syncUser)
    {
        try
        {
            var args = new ImportCategoryEventArgs(categoryContent, importCategory, create, false, false);

            OnCategorySaveStarting(this, args).GetAwaiter().GetResult();

            var saveImages = args.ImagesHaveNoChanges ? false : ImportMedia(categoryContent, importCategory.Images, allUmbracoMedia, mediaIndex, preserveExistingValues: importCategory.PreserveExistingValues);

            var compareValue = importCategory.Comparer ?? ComputeSha256Hash(importCategory, new string[] { "SubCategories", "Images", "Products", "EventProperties" });

            // If no changes are found and not creating then return,
            if (!HasContentChanges(categoryContent.GetValue<string>("comparer"), compareValue) && !args.IsCreateOperation && !saveImages)
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
                    if (categoryContent.HasProperty(property.Key))
                        categoryContent.SetValue(property.Key, property.Value);
                }
            }

            if (importCategory.SortOrder.HasValue)
            {
                categoryContent.SortOrder = importCategory.SortOrder.Value;
            }
            
            categoryContent.SetValue("comparer", compareValue);

            categoryContent.Name = importCategory.NodeName;

            if (importCategory.TemplateId.HasValue)
            {
                categoryContent.TemplateId = importCategory.TemplateId.Value;
            }

            SaveEvent(categoryContent, importCategory.SaveEvent, importCategory.PreservePublishStatus, syncUser, args.IsCreateOperation);

            categoriesSaved.Add(importCategory);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save Category: {importCategory.Identifier} Message: {ex.Message}");
            throw;
        }

    }
    private void SaveProduct(IContent productContent, ImportProduct importProduct, List<IContent>? allUmbracoCategories, List<IMedia>? allUmbracoMedia, ImportMediaIndex? mediaIndex, bool create, int syncUser, IContent? recycleBinNode = null, IContent? productProcessNode = null, bool forceUpdate = false)
    {
        try
        {
            var args = new ImportProductEventArgs(productContent, importProduct, create, false, false);

            OnProductSaveStarting(this, args).GetAwaiter().GetResult();

            var perStoreStock = Configuration.Instance.PerStoreStock;

            // Always do stock update
            if (importProduct.Stock.Any())
            {
                foreach (var stock in importProduct.Stock)
                {
                    var currentStock = perStoreStock ? _stock.GetStock(productContent.Key, stock.StoreAlias) : _stock.GetStock(productContent.Key);
                    var newStock = stock.Stock >= 0 ? stock.Stock : 0;

                    if (newStock != currentStock)
                    {
                        if (perStoreStock) 
                        {
                            _ = _stock.SetStockAsync(productContent.Key, stock.StoreAlias, newStock); 
                        }
                        else
                        {
                            _ = _stock.SetStockAsync(productContent.Key, newStock);
                        }
                    }
                }
            }

            var saveImages = false;
            var saveFiles = false;

            if (allUmbracoMedia is not null)
            {
                if (!args.ImagesHaveNoChanges)
                {
                    saveImages = ImportMedia(productContent, importProduct.Images, allUmbracoMedia, mediaIndex, preserveExistingValues: importProduct.PreserveExistingValues);
                }

                if (!args.FilesHaveNoChanges)
                {
                    saveFiles = ImportMedia(productContent, importProduct.Files, allUmbracoMedia, mediaIndex, ImportMediaTypes.File, ImportMediaContentTypes.files, preserveExistingValues: importProduct.PreserveExistingValues);
                }
            }

            var compareValue = importProduct.Comparer ?? ComputeSha256Hash(importProduct, new string[] { "VariantGroups", "Images", "EventProperties", "Files", "Stock", "UpdateSlug" });

            // If no changes are found and not creating then return,
            if (!forceUpdate && !HasContentChanges(productContent.GetValue<string>("comparer"), compareValue) && !args.IsCreateOperation && !saveImages && !saveFiles)
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

            if (!importProduct.PreserveExistingValues)
            {
                productContent.SetProperty("description", importProduct.Description);
            }

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

            if (importProduct.SortOrder.HasValue)
            {
                productContent.SortOrder = importProduct.SortOrder.Value;
            }

            if (importProduct.AdditionalProperties != null && importProduct.AdditionalProperties.Any())
            {
                foreach (var property in importProduct.AdditionalProperties)
                {
                    if (productContent.HasProperty(property.Key))
                    {
                        productContent.SetValue(property.Key, property.Value);
                    }
                }
            }

            if (!importProduct.PreserveExistingValues || create)
            {
                if (importProduct.Categories.Count > 1 && allUmbracoCategories != null)
                {
                    var categoryIdentifiers = new HashSet<string>(importProduct.Categories.Skip(1));

                    var umbracoCategories = allUmbracoCategories
                        .Where(x => categoryIdentifiers.Contains(x.GetValue<string>(Configuration.ImportAliasIdentifier) ?? ""))
                        .ToList();

                    var udis = umbracoCategories.Select(x => x.GetUdi());

                    var stringUdis = string.Join(",", udis.Select(x => x.ToString()));

                    productContent.SetValue("categories", stringUdis);
                } else
                {
                    productContent.SetValue("categories", "");
                }
            }

            if (importProduct.EnableBackorder)
            {
                productContent.SetValue("enableBackorder", importProduct.EnableBackorder);
            }

            productContent.SetValue("comparer", compareValue);

            productContent.Name = importProduct.NodeName;

            if (importProduct.TemplateId.HasValue)
            {
                productContent.TemplateId = importProduct.TemplateId.Value;
            }

            SaveEvent(productContent, importProduct.SaveEvent, importProduct.PreservePublishStatus, syncUser, args.IsCreateOperation, recycleBinNode, productProcessNode);

            productsSaved.Add(importProduct);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save Product: Sku: {importProduct.SKU} Message: {ex.Message}");
            importProduct.Exception = ex;
            productsSaved.Add(importProduct);
        }
    }
    private void SaveVariantGroup(IContent variantGroupContent, ImportVariantGroup importVariantGroup, List<IMedia> allUmbracoMedia, ImportMediaIndex mediaIndex, bool create, int syncUser, bool forceUpdate = false)
    {
        var saveImages = ImportMedia(variantGroupContent, importVariantGroup.Images, allUmbracoMedia, mediaIndex, preserveExistingValues: importVariantGroup.PreserveExistingValues);

        var compareValue = importVariantGroup.Comparer ?? ComputeSha256Hash(importVariantGroup, new string[] { "Variants", "Images", "EventProperties" });

        // If no changes are found and not creating then return,
        if (!forceUpdate && !HasContentChanges(variantGroupContent.GetValue<string>("comparer"), compareValue) && !create && !saveImages)
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

        if (importVariantGroup.SortOrder.HasValue)
        {
            variantGroupContent.SortOrder = importVariantGroup.SortOrder.Value;
        }

        variantGroupContent.SetValue("comparer", compareValue);

        variantGroupContent.Name = importVariantGroup.NodeName;

        SaveEvent(variantGroupContent, importVariantGroup.SaveEvent, importVariantGroup.PreservePublishStatus, syncUser, create);

        variantGroupsSaved.Add(importVariantGroup);
    }
    private void SaveVariant(IContent variantContent, ImportVariant importVariant, List<IMedia>? allUmbracoMedia, ImportMediaIndex? mediaIndex, bool create, int syncUser, bool forceUpdate = false)
    {
        var args = new ImportVariantEventArgs(variantContent, importVariant, create, false, false);

        OnVariantSaveStarting(this, args).GetAwaiter().GetResult();

        var perStoreStock = Configuration.Instance.PerStoreStock;

        // Always do stock update
        if (importVariant.Stock.Any())
        {
            foreach (var stock in importVariant.Stock)
            {
                var currentStock = perStoreStock ? _stock.GetStock(variantContent.Key, stock.StoreAlias) : _stock.GetStock(variantContent.Key);
                var newStock = stock.Stock >= 0 ? stock.Stock : 0;

                // Only update if we find change 
                if (newStock != currentStock)
                {
                    if (perStoreStock)
                    {
                        _ = _stock.SetStockAsync(variantContent.Key, stock.StoreAlias, newStock);
                    }
                    else
                    {
                        _ = _stock.SetStockAsync(variantContent.Key, newStock);
                    }
                }
            }
        }

        var saveImages = false;
        var saveFiles = false;

        if (allUmbracoMedia != null)
        {
            saveImages = args.ImagesHaveNoChanges ? false : ImportMedia(variantContent, importVariant.Images, allUmbracoMedia, mediaIndex, preserveExistingValues: importVariant.PreserveExistingValues);

            saveFiles = args.FilesHaveNoChanges ? false : ImportMedia(variantContent, importVariant.Files, allUmbracoMedia, mediaIndex, ImportMediaTypes.File, ImportMediaContentTypes.files, preserveExistingValues: importVariant.PreserveExistingValues);
        }

        var compareValue = importVariant.Comparer ?? ComputeSha256Hash(importVariant, new string[] { "Images", "EventProperties", "Files", "Stock" });

        // If no changes are found and not creating then return,
        if (!forceUpdate && !HasContentChanges(variantContent.GetValue<string>("comparer"), compareValue) && !args.IsCreateOperation && !saveImages && !saveFiles)
        {
            return;
        }

        variantContent.SetProperty("title", importVariant.Title);

        if (!string.IsNullOrEmpty(importVariant.SKU))
        {
            variantContent.SetValue("sku", importVariant.SKU);
        }

        if (variantContent.HasProperty("description") && !importVariant.PreserveExistingValues)
        {
            variantContent.SetProperty("description", importVariant.Description);
        }

        if (importVariant.SortOrder.HasValue)
        {
            variantContent.SortOrder = importVariant.SortOrder.Value;
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

        SaveEvent(variantContent, importVariant.SaveEvent, importVariant.PreservePublishStatus, syncUser, args.IsCreateOperation);

        variantsSaved.Add(importVariant);
        
    }

    private bool ImportMedia(IContent content, List<IImportMedia> importMedias, List<IMedia>? allUmbracoMedia, ImportMediaIndex? mediaIndex, ImportMediaTypes mediaType = ImportMediaTypes.Image, ImportMediaContentTypes contentTypeAlias = ImportMediaContentTypes.images, bool saveContent = false, int syncUser = -1, bool preserveExistingValues = false)
    {
        if (allUmbracoMedia == null)
        {
            return false;
        }

        mediaIndex ??= new ImportMediaIndex(allUmbracoMedia);

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

        if (preserveExistingValues && currentImagesCount > 0)
        {
            return false;
        }

        var newImages = new List<string>();

        var sortedMedias = importMedias
            .Select((media, index) => new { media, index })
            .OrderBy(x => x.media.SortOrder.HasValue ? x.media.SortOrder.Value : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => x.media);

        foreach (var media in sortedMedias)
        {
            if (media is ImportMediaFromUdi importMedia)
            {
                AddUdiIfNotExist(newImages, importMedia.Udi);
            }
            else if (media is ImportMediaFromExternalUrl externalUrlMedia)
            {
                var compareValue = externalUrlMedia.Comparer ?? ComputeSha256Hash(externalUrlMedia, new string[] { "Action", "SortOrder" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, externalUrlMedia.Identifier, compareValue);

                if (media.Action == ImportMediaAction.Add)
                {
                    if (umbMedia == null)
                    {
                        // Create
                        umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier, syncUser);

                        if (umbMedia != null)
                        {
                            allUmbracoMedia.Add(umbMedia);
                            mediaIndex.Add(umbMedia);
                            AddUdiIfNotExist(newImages, umbMedia.GetUdi().ToString());
                        }

                    } else
                    {   // Update


                        umbMedia = mediaIndex.FindByComparer(mediaType, compareValue);

                        // If the media is not found by comparer we need to create a new media
                        if (umbMedia == null)
                        {
                            umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier, syncUser);

                            if (umbMedia != null)
                            {
                                allUmbracoMedia.Add(umbMedia);
                                mediaIndex.Add(umbMedia);
                                AddUdiIfNotExist(newImages, umbMedia.GetUdi().ToString());
                            }

                        } else
                        {
                            // If comparer is the same probably the sort order has just changed so we just want to change the order.
                            _importMediaService.UpdateMediaSortOrder(umbMedia, externalUrlMedia);
                            AddUdiIfNotExist(newImages, umbMedia.GetUdi().ToString());
                        }

                    } 

                } else if (media.Action == ImportMediaAction.Delete) {

                    if (umbMedia != null)
                    {
                        RemoveUdiIfExist(newImages, umbMedia.GetUdi().ToString());
                    }
                }

            }
            else if (media is ImportMediaFromBytes bytesMedia)
            {
                var compareValue = bytesMedia.Comparer ?? ComputeSha256Hash(bytesMedia, new string[] { "Bytes", "SortOrder" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, bytesMedia.Identifier, compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBytes(bytesMedia, compareValue, mediaType, bytesMedia.Identifier, syncUser);
                    allUmbracoMedia.Add(umbMedia);
                    mediaIndex.Add(umbMedia);
                }

                AddUdiIfNotExist(newImages, umbMedia.GetUdi().ToString());
            }
            else if (media is ImportMediaFromBase64 base64Media)
            {
                var compareValue = base64Media.Comparer ?? ComputeSha256Hash(base64Media, new string[] { "Base64", "SortOrder" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, base64Media.Identifier, compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBase64(base64Media, compareValue, mediaType, base64Media.Identifier, syncUser);
                    allUmbracoMedia.Add(umbMedia);
                    mediaIndex.Add(umbMedia);
                }

                AddUdiIfNotExist(newImages, umbMedia.GetUdi().ToString());
            }
        }

        if (newImages.Count == 0 && currentImagesCount == 0)
        {
            return false;
        }

        var importedImages = SortImages(newImages.DistinctBy(x => x).ToList(), allUmbracoMedia, mediaIndex);

        if (currentImages != importedImages)
        {
            content.SetValue(contentTypeAlias.ToString(), importedImages);

            if (saveContent)
            {

                if (content.Published)
                {
                    _contentService.SaveAndPublish(content, userId: syncUser);
                } else
                {
                    _contentService.Save(content, userId: syncUser);
                }
                
            }

            return true;
        }
        return false;
    }
    private bool ImportSingleMedia(IContent content, List<IImportMedia> importMedias, List<IMedia>? allUmbracoMedia, ImportMediaIndex? mediaIndex, ImportMediaTypes mediaType = ImportMediaTypes.Image, ImportMediaContentTypes contentTypeAlias = ImportMediaContentTypes.images, bool saveContent = false, int syncUser = -1)
    {

        if (allUmbracoMedia == null)
        {
            return false;
        }

        mediaIndex ??= new ImportMediaIndex(allUmbracoMedia);

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
            }
            catch
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
                }
                else if (media.Action == ImportMediaAction.Delete)
                {
                    RemoveUdiIfExist(currentImagesUdi, importMedia.Udi);
                }
            }
            else if (media is ImportMediaFromExternalUrl externalUrlMedia)
            {
                var compareValue = externalUrlMedia.Comparer ?? ComputeSha256Hash(externalUrlMedia, new string[] { "Action", "SortOrder" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, externalUrlMedia.Identifier, compareValue);

                if (media.Action == ImportMediaAction.Add)
                {
                    if (umbMedia == null)
                    {
                        // Create
                        umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier, syncUser);

                        if (umbMedia != null)
                        {
                            allUmbracoMedia.Add(umbMedia);
                            mediaIndex.Add(umbMedia);
                            AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                        }

                    }
                    else
                    {   // Update

                        // Remove media
                        RemoveUdiIfExist(currentImagesUdi, umbMedia.GetUdi().ToString());

                        umbMedia = mediaIndex.FindByComparer(mediaType, compareValue);

                        // If the media is not found by comparer we need to create a new media
                        if (umbMedia == null)
                        {
                            umbMedia = _importMediaService.ImportMediaFromExternalUrl(externalUrlMedia, compareValue, mediaType, externalUrlMedia.Identifier, syncUser);

                            if (umbMedia != null)
                            {
                                allUmbracoMedia.Add(umbMedia);
                                mediaIndex.Add(umbMedia);
                                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                            }

                        }
                        else
                        {
                            // If comparer is the same probably the sort order has just changed so we just want to change the order.
                            _importMediaService.UpdateMediaSortOrder(umbMedia, externalUrlMedia);
                            AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                        }

                    }

                }
                else if (media.Action == ImportMediaAction.Delete)
                {

                    if (umbMedia != null)
                    {
                        RemoveUdiIfExist(currentImagesUdi, umbMedia.GetUdi().ToString());
                    }
                }

            }
            else if (media is ImportMediaFromBytes bytesMedia)
            {
                var compareValue = bytesMedia.Comparer ?? ComputeSha256Hash(bytesMedia, new string[] { "Bytes" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, bytesMedia.Identifier, compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBytes(bytesMedia, compareValue, mediaType, bytesMedia.Identifier, syncUser);
                    allUmbracoMedia.Add(umbMedia);
                    mediaIndex.Add(umbMedia);
                }

                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
            }
            else if (media is ImportMediaFromBase64 base64Media)
            {
                var compareValue = base64Media.Comparer ?? ComputeSha256Hash(base64Media, new string[] { "Base64" });

                var umbMedia = mediaIndex.FindByIdentifierOrComparer(mediaType, base64Media.Identifier, compareValue);

                if (umbMedia == null)
                {
                    umbMedia = _importMediaService.ImportMediaFromBase64(base64Media, compareValue, mediaType, base64Media.Identifier, syncUser);
                    allUmbracoMedia.Add(umbMedia);
                    mediaIndex.Add(umbMedia);
                }

                AddUdiIfNotExist(currentImagesUdi, umbMedia.GetUdi().ToString());
            }
        }

        if (currentImagesCount == 0 && currentImagesUdi.Count == 0)
        {
            return false;
        }

        var importedImages = SortImages(currentImagesUdi.DistinctBy(x => x).ToList(), allUmbracoMedia, mediaIndex);

        if (currentImages != importedImages)
        {
            content.SetValue(contentTypeAlias.ToString(), importedImages);

            if (saveContent)
            {

                if (content.Published)
                {
                    _contentService.SaveAndPublish(content, userId: syncUser);
                }
                else
                {
                    _contentService.Save(content, userId: syncUser);
                }
                
            }

            return true;
        }
        return false;
    }
    private string SortImages(List<string> imagesUdi, List<IMedia> allUmbracoMedia, ImportMediaIndex? mediaIndex = null)
    {
        Dictionary<string, IMedia>? mediaDictionary = null;
        if (mediaIndex == null)
        {
            // Convert allUmbracoMedia to a dictionary for fast lookup
            mediaDictionary = allUmbracoMedia.ToDictionary(
                media => "umb://media/" + media.Key.ToString().Replace("-", "", StringComparison.InvariantCultureIgnoreCase)
            );
        }

        var images = new List<(IMedia Media, int SortOrder)>();

        // Populate images list with media and sort order
        foreach (var imageUdi in imagesUdi)
        {
            var imageNode = mediaIndex?.FindByUdi(imageUdi);
            if (imageNode != null || mediaDictionary?.TryGetValue(imageUdi, out imageNode) == true)
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

    private void SaveEvent(
        IContent content,
        ImportSaveEntEnum saveEvent,
        bool preservePublishStatus,
        int syncUser,
        bool create,
        IContent? recycleBinNode = null,
        IContent? productProcessNode = null)
    {
        // If we should be in the processing area (e.g., coming from recycle bin), move FIRST.
        if (recycleBinNode != null
            && productProcessNode != null
            && content.ContentType.Alias == "ekmProduct"
            && content.ParentId == recycleBinNode.Id)
        {
            _contentService.Move(content, productProcessNode.Id, syncUser);
        }

        // If content currently lives under productProcessNode, we treat it as staging: never publish here.
        bool inProcessing = productProcessNode != null && content.ParentId == productProcessNode.Id;

        if (inProcessing)
        {
            // Always just save while in processing area.
            _contentService.Save(content, userId: syncUser);
            return;
        }

        switch (saveEvent)
        {
            case ImportSaveEntEnum.Unpublish:
                if (create)
                {
                    _contentService.Save(content, userId: syncUser);
                }
                else
                {
                    _contentService.Save(content, userId: syncUser);
                    _contentService.Unpublish(content, userId: syncUser);
                }
                return;

            case ImportSaveEntEnum.SavePublish:
            default:
                if (preservePublishStatus)
                {
                    if (content.Published)
                    {
                        _contentService.SaveAndPublish(content, userId: syncUser);
                    }
                    else
                    {
                        _contentService.Save(content, userId: syncUser);
                    }
                }
                else
                {
                    _contentService.SaveAndPublish(content, userId: syncUser);
                }
                return;
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

    private static Dictionary<string, IContent> BuildContentByIdentifier(IEnumerable<IContent> contents)
    {
        var indexed = new Dictionary<string, IContent>();

        foreach (var content in contents)
        {
            if (!content.HasProperty(Configuration.ImportAliasIdentifier))
                continue;

            var identifier = content.GetValue<string>(Configuration.ImportAliasIdentifier);
            if (string.IsNullOrEmpty(identifier))
                continue;

            indexed.TryAdd(identifier, content);
        }

        return indexed;
    }

    private static Dictionary<int, List<IContent>> BuildChildrenByParentId(IEnumerable<IContent> contents)
    {
        var indexed = new Dictionary<int, List<IContent>>();

        foreach (var content in contents)
        {
            GetIndexedChildren(indexed, content.ParentId).Add(content);
        }

        return indexed;
    }

    private static List<IContent> GetIndexedChildren(Dictionary<int, List<IContent>> indexed, int parentId)
    {
        if (!indexed.TryGetValue(parentId, out var children))
        {
            children = new List<IContent>();
            indexed[parentId] = children;
        }

        return children;
    }

    private static void MoveIndexedContent(Dictionary<int, List<IContent>> indexed, IContent content, int oldParentId, int newParentId)
    {
        if (indexed.TryGetValue(oldParentId, out var oldChildren))
        {
            oldChildren.Remove(content);
        }

        GetIndexedChildren(indexed, newParentId).Add(content);
    }

    /// <summary>
    /// Gets an existing content item from a list of Umbraco content by its identifier or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="contenType">The content type to create</param>
    /// <param name="allContent">The list of all content items to search through.</param>
    /// <param name="nodeName">The name for the new content node if creation is needed.</param>
    /// <param name="identifer">The identifier to search for in the existing content items. Cannot be null.</param>
    /// <param name="parentContent">The parent content under which the new content should be created if needed.</param>
    /// <param name="syncUser">Id of the user doing changes</param>
    /// <param name="create">Outputs true if a new content item was created, false otherwise.</param>
    /// <returns>The found or newly created content item.</returns>
    private IContent? GetOrCreateContent(IContentType? contenType, List<IContent> allContent, string nodeName, string identifer, IContent parentContent, int syncUser, out bool create)
    {
        ArgumentNullException.ThrowIfNull(contenType);
        ArgumentNullException.ThrowIfNull(nodeName);
        ArgumentNullException.ThrowIfNull(identifer);
        ArgumentNullException.ThrowIfNull(parentContent);

        create = false;
        var content = allContent.FirstOrDefault(x => x.GetValue<string>(Configuration.ImportAliasIdentifier) == identifer);

        if (content != null && content.HasProperty("ekmDisableSync") && content.GetValue<bool>("ekmDisableSync"))
        {
            return null;
        }

        if (content == null)
        {
            try
            {
                content = _contentService.Create(nodeName, parentContent.Id, contenType, syncUser);
                create = true;
            } catch(Exception ex)
            {
                _logger.LogError(ex, $"Could not create {contenType.Alias} node with name {nodeName} under parent {parentContent.Id}. Message: {ex.Message}");
            }

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

    private List<IContent> GetAllUmbracoProducts()
    {
        ArgumentNullException.ThrowIfNull(productContentType);

        var categories = _contentService
            .GetPagedOfType(productContentType.Id, 0, int.MaxValue, out var _, new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed))
            .ToList();

        return categories;
    }
    private List<IContent> GetAllEkomNodes(int pageSize = 2_000)
    {
        ArgumentNullException.ThrowIfNull(umbracoRootContent);

        var all = new List<IContent>(capacity: pageSize * 2);
        if (!umbracoRootContent.Trashed && !umbracoRootContent.GetValue<bool>("ekmDisableSync"))
            all.Add(umbracoRootContent);

        var filter = new Query<IContent>(_scopeProvider.SqlContext)
            .Where(x => !x.Trashed);

        long pageIndex = 0;
        long descendantCount = 0;

        while (true)
        {
            var batch = _contentService.GetPagedDescendants(
                umbracoRootContent.Id,
                pageIndex,
                pageSize,
                out var totalRecords,
                filter);

            var list = batch as IList<IContent> ?? batch.ToList();
            if (list.Count == 0)
                break;

            descendantCount += list.Count;
            all.AddRange(list.Where(x => !x.GetValue<bool>("ekmDisableSync")));

            if (descendantCount >= totalRecords)
                break;

            pageIndex++;
        }

        return all;
    }

    private sealed class ImportMediaIndex
    {
        private readonly Dictionary<(string MediaType, string Identifier), IMedia> _mediaByIdentifier = new();
        private readonly Dictionary<(string MediaType, string Comparer), IMedia> _mediaByComparer = new();
        private readonly Dictionary<string, IMedia> _mediaByUdi = new();

        public ImportMediaIndex(IEnumerable<IMedia> mediaItems)
        {
            foreach (var media in mediaItems)
            {
                Add(media);
            }
        }

        public void Add(IMedia media)
        {
            var mediaType = media.ContentType.Alias;
            _mediaByUdi.TryAdd(media.GetUdi().ToString(), media);

            if (media.HasProperty("ekmIdentifier"))
            {
                var identifier = media.GetValue<string>("ekmIdentifier");
                if (!string.IsNullOrEmpty(identifier))
                {
                    _mediaByIdentifier.TryAdd((mediaType, identifier), media);
                }
            }

            if (media.HasProperty("comparer"))
            {
                var comparer = media.GetValue<string>("comparer");
                if (!string.IsNullOrEmpty(comparer))
                {
                    _mediaByComparer.TryAdd((mediaType, comparer), media);
                }
            }
        }

        public IMedia? FindByIdentifierOrComparer(ImportMediaTypes mediaType, string? identifier, string comparer)
        {
            var mediaTypeAlias = mediaType.ToString();

            if (!string.IsNullOrEmpty(identifier) && _mediaByIdentifier.TryGetValue((mediaTypeAlias, identifier), out var mediaByIdentifier))
            {
                return mediaByIdentifier;
            }

            return FindByComparer(mediaType, comparer);
        }

        public IMedia? FindByComparer(ImportMediaTypes mediaType, string comparer)
        {
            return _mediaByComparer.TryGetValue((mediaType.ToString(), comparer), out var media)
                ? media
                : null;
        }

        public IMedia? FindByUdi(string udi)
        {
            return _mediaByUdi.TryGetValue(udi, out var media)
                ? media
                : null;
        }
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

    public List<ImportCategory>? GetAllCategories(ImportData importData) =>
        importData.Categories == null
            ? null
            : importData.Categories.SelectMany(GetFlattenedCategories).ToList();


    private IEnumerable<ImportCategory> GetFlattenedCategories(ImportCategory category) =>
        new[] { category }.Concat(category.SubCategories?.SelectMany(GetFlattenedCategories) ?? Enumerable.Empty<ImportCategory>());
}
