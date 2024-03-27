using Ekom.Models.Import;

namespace Ekom.Services
{
    public interface IImportService
    {
        public void FullSync(ImportData data);
        public void CategorySync(ImportCategory categoryData);
        public void ProductSync(ImportProduct productData);
    }
}
