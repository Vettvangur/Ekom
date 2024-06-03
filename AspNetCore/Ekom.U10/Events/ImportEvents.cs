using Ekom.Models.Import;
using Ekom.Utilities;
using Umbraco.Cms.Core.Models;

namespace Ekom.Events
{
    public static class ImportEvents
    {
        public static event EventHandler<ImportCategoryEventArgs> CategorySaveStarting;
        internal static void OnCategorySaveStarting(object sender, ImportCategoryEventArgs args)
            => CategorySaveStarting?.Invoke(sender, args);

        public static event EventHandler<ImportProductEventArgs> ProductSaveStarting;
        internal static void OnProductSaveStarting(object sender, ImportProductEventArgs args)
            => ProductSaveStarting?.Invoke(sender, args);

        public static event EventHandler<ImportVariantEventArgs> VariantSaveStarting;
        internal static void OnVariantSaveStarting(object sender, ImportVariantEventArgs args)
            => VariantSaveStarting?.Invoke(sender, args);

        public static event EventHandler<ImportSyncFinishedEventArgs> SyncFinished;
        internal static void OnSyncFinished(object sender, ImportSyncFinishedEventArgs args)
            => SyncFinished?.Invoke(sender, args);
    }

    public class ImportCategoryEventArgs : EventArgs
    {
        public IContent CategoryContent { get; }
        public ImportCategory ImportCategory { get; }
        public bool IsCreateOperation { get; }

        public ImportCategoryEventArgs(IContent categoryContent, ImportCategory importCategory, bool isCreateOperation)
        {
            CategoryContent = categoryContent;
            IsCreateOperation = isCreateOperation;
            ImportCategory = importCategory;
        }
    }

    public class ImportProductEventArgs : EventArgs
    {
        public IContent ProductContent { get; }
        public ImportProduct ImportProduct { get; }
        public bool IsCreateOperation { get; }

        public ImportProductEventArgs(IContent productContent, ImportProduct importProduct, bool isCreateOperation)
        {
            ProductContent = productContent;
            ImportProduct = importProduct;
            IsCreateOperation = isCreateOperation;
        }
    }

    public class ImportVariantEventArgs : EventArgs
    {
        public IContent VariantContent { get; }
        public ImportVariant ImportVariant { get; }
        public bool IsCreateOperation { get; }

        public ImportVariantEventArgs(IContent variantContent, ImportVariant importVariant, bool isCreateOperation)
        {
            VariantContent = variantContent;
            ImportVariant = importVariant;
            IsCreateOperation = isCreateOperation;
        }
    }

    public class ImportSyncFinishedEventArgs : EventArgs
    {
        public List<ImportCategory> ImportCategories { get; }
        public List<ImportProduct> ImportProducts { get; }
        public List<ImportVariant> ImportVariants { get; }
        public List<ImportVariantGroup> ImportVariantGroups { get; }
        public ImportSyncType Type { get; }

        public ImportSyncFinishedEventArgs(List<ImportCategory> importCategories, List<ImportProduct> importProducts, List<ImportVariant> importVariants, List<ImportVariantGroup> importVariantGroups, ImportSyncType type)
        {
            ImportCategories = importCategories;
            ImportProducts = importProducts;
            ImportVariants = importVariants;
            ImportVariantGroups = importVariantGroups;
            Type = type;
        }
    }
}

