using Ekom.Models.Import;
using Ekom.Utilities;
using Umbraco.Cms.Core.Models;

namespace Ekom.Events;

public static class ImportEvents
{
    public static event Func<ImportCategoryEventArgs, Task>? CategorySaveStarting;

    internal static async Task OnCategorySaveStarting(object sender, ImportCategoryEventArgs args)
    {
        if (CategorySaveStarting == null)
        {
            return;
        }

        foreach (var handler in CategorySaveStarting.GetInvocationList())
        {
            await ((Func<ImportCategoryEventArgs, Task>)handler)(args).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Evaluates product eligibility before product reconciliation and saving.
    /// </summary>
    public static event Func<ImportProductEvaluatingEventArgs, Task>? ProductImportEvaluating;

    internal static async Task OnProductImportEvaluating(object sender, ImportProductEvaluatingEventArgs args)
    {
        var handlers = ProductImportEvaluating;
        if (handlers == null)
            return;

        foreach (var handler in handlers.GetInvocationList())
        {
            await ((Func<ImportProductEvaluatingEventArgs, Task>)handler)(args).ConfigureAwait(false);
        }
    }

    public static event Func<ImportProductEventArgs, Task>? ProductSaveStarting;

    internal static async Task OnProductSaveStarting(object sender, ImportProductEventArgs args)
    {
        if (ProductSaveStarting == null)
        {
            return;
        }

        foreach (var handler in ProductSaveStarting.GetInvocationList())
        {
            await ((Func<ImportProductEventArgs, Task>)handler)(args).ConfigureAwait(false);
        }
    }

    public static event Func<ImportVariantEventArgs, Task>? VariantSaveStarting;

    internal static async Task OnVariantSaveStarting(object sender, ImportVariantEventArgs args)
    {
        if (VariantSaveStarting == null)
        {
            return;
        }

        foreach (var handler in VariantSaveStarting.GetInvocationList())
        {
            await ((Func<ImportVariantEventArgs, Task>)handler)(args).ConfigureAwait(false);
        }
    }

    public static event Func<ImportSyncFinishedEventArgs, Task>? SyncFinished;

    internal static async Task OnSyncFinished(object sender, ImportSyncFinishedEventArgs args)
    {
        if (SyncFinished == null)
        {
            return;
        }

        foreach (var handler in SyncFinished.GetInvocationList())
        {
            await ((Func<ImportSyncFinishedEventArgs, Task>)handler)(args).ConfigureAwait(false);
        }
    }
}

public class ImportCategoryEventArgs : EventArgs
{
    public ImportCategoryEventArgs(IContent categoryContent, ImportCategory importCategory, bool isCreateOperation, bool imagesHaveNoChanges, bool filesHaveNoChanges)
    {
        CategoryContent = categoryContent;
        ImportCategory = importCategory;
        IsCreateOperation = isCreateOperation;
        ImagesHaveNoChanges = imagesHaveNoChanges;
    }

    public IContent CategoryContent { get; }

    public ImportCategory ImportCategory { get; }

    public bool IsCreateOperation { get; }

    public bool ImagesHaveNoChanges { get; set; }
}

/// <summary>
/// Excluding a product removes it from the effective incoming import. With missing-product
/// removal enabled, an existing excluded product is treated as missing. Single-product
/// imports skip excluded products without removing existing content.
/// </summary>
public class ImportProductEvaluatingEventArgs : EventArgs
{
    private bool _excludeFromImport;

    public ImportProductEvaluatingEventArgs(ImportProduct importProduct, IContent? productContent, Guid importRootKey)
    {
        ImportProduct = importProduct;
        ProductContent = productContent;
        ImportRootKey = importRootKey;
    }

    public ImportProduct ImportProduct { get; }

    /// <summary>The matching existing node available to the current import lookup, or null.</summary>
    public IContent? ProductContent { get; }

    public Guid ImportRootKey { get; }

    /// <summary>Once true, exclusion cannot be reversed by a later subscriber.</summary>
    public bool ExcludeFromImport
    {
        get => _excludeFromImport;
        set => _excludeFromImport |= value;
    }
}

public class ImportProductEventArgs : EventArgs
{
    public ImportProductEventArgs(IContent productContent, ImportProduct importProduct, bool isCreateOperation, bool imagesHaveNoChanges, bool filesHaveNoChanges)
    {
        ProductContent = productContent;
        ImportProduct = importProduct;
        IsCreateOperation = isCreateOperation;
        ImagesHaveNoChanges = imagesHaveNoChanges;
        FilesHaveNoChanges = filesHaveNoChanges;
    }

    public IContent ProductContent { get; }

    public ImportProduct ImportProduct { get; }

    public bool IsCreateOperation { get; }

    public bool ImagesHaveNoChanges { get; set; }

    public bool FilesHaveNoChanges { get; set; }
}

public class ImportVariantEventArgs : EventArgs
{
    public ImportVariantEventArgs(IContent variantContent, ImportVariant importVariant, bool isCreateOperation, bool imagesHaveNoChanges, bool filesHaveNoChanges)
    {
        VariantContent = variantContent;
        ImportVariant = importVariant;
        IsCreateOperation = isCreateOperation;
        ImagesHaveNoChanges = imagesHaveNoChanges;
        FilesHaveNoChanges = filesHaveNoChanges;
    }

    public IContent VariantContent { get; }

    public ImportVariant ImportVariant { get; }

    public bool IsCreateOperation { get; }

    public bool ImagesHaveNoChanges { get; set; }

    public bool FilesHaveNoChanges { get; set; }
}

public class ImportSyncFinishedEventArgs : EventArgs
{
    public ImportSyncFinishedEventArgs(List<ImportCategory> importCategories, List<ImportProduct> importProducts, List<ImportVariant> importVariants, List<ImportVariantGroup> importVariantGroups, ImportSyncType type)
    {
        ImportCategories = importCategories;
        ImportProducts = importProducts;
        ImportVariants = importVariants;
        ImportVariantGroups = importVariantGroups;
        Type = type;
    }

    public List<ImportCategory> ImportCategories { get; }

    public List<ImportProduct> ImportProducts { get; }

    public List<ImportVariant> ImportVariants { get; }

    public List<ImportVariantGroup> ImportVariantGroups { get; }

    public ImportSyncType Type { get; }
}
