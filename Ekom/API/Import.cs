using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using static LinqToDB.Common.Configuration;

namespace Ekom.API;

/// <summary>
/// The Import API, grants access to import all ecommerce data into umbraco
/// </summary>
public class Import
{
    /// <summary>
    /// Catalog Instance
    /// </summary>
    public static Import Instance => Configuration.Resolver.GetService<Import>();

    private readonly IImportService _importService;

    public Import(IImportService importService)
    {
        _importService = importService;
    }

    public void FullSync(ImportData data)
    {
        _importService.FullSync(data);
    }

    public void CategorySync(ImportCategory categoryData)
    {
        _importService.CategorySync(categoryData);
    }

    public void ProductSync(ImportProduct productData)
    {
        _importService.ProductSync(productData);
    }
}
