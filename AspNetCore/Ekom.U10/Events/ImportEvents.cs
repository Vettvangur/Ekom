using Ekom.Models.Import;
using Ekom.Utilities;
using Umbraco.Cms.Core.Models;

namespace Ekom.Events
{
    public static class ImportEvents
    {
        public static event Func<ImportCategoryEventArgs, Task> CategorySaveStarting;
        internal static async Task OnCategorySaveStarting(object sender, ImportCategoryEventArgs args)
        {
            if (CategorySaveStarting != null)
            {
                foreach (var handler in CategorySaveStarting.GetInvocationList())
                {
                    await ((Func<ImportCategoryEventArgs, Task>)handler)(args);
                }
            }
        }

        public static event Func<ImportProductEventArgs, Task> ProductSaveStarting;
        internal static async Task OnProductSaveStarting(object sender, ImportProductEventArgs args)
        {
            if (ProductSaveStarting != null)
            {
                foreach (var handler in ProductSaveStarting.GetInvocationList())
                {
                    await ((Func<ImportProductEventArgs, Task>)handler)(args);
                }
            }
        }

        public static event Func<ImportVariantEventArgs, Task> VariantSaveStarting;
        internal static async Task OnVariantSaveStarting(object sender, ImportVariantEventArgs args)
        {
            if (VariantSaveStarting != null)
            {
                foreach (var handler in VariantSaveStarting.GetInvocationList())
                {
                    await ((Func<ImportVariantEventArgs, Task>)handler)(args);
                }
            }
        }

        public static event Func<ImportSyncFinishedEventArgs, Task> SyncFinished;
        internal static async Task OnSyncFinished(object sender, ImportSyncFinishedEventArgs args)
        {
            if (SyncFinished != null)
            {
                foreach (var handler in SyncFinished.GetInvocationList())
                {
                    await ((Func<ImportSyncFinishedEventArgs, Task>)handler)(args);
                }
            }
        }
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
