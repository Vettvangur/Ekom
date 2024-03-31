using Ekom.Models.Import;
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
}

