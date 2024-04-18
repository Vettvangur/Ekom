using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Ekom.Site.Controllers;

public class ImportController : UmbracoAuthorizedApiController
{
    private readonly IImportService _importService;
    private readonly IShortStringHelper _shortStringHelper;

    private readonly Guid rootCategory = new Guid("f4294c2d-b64a-4173-9f45-30ce0b9db220");
    private readonly List<ImportProduct> products = new List<ImportProduct>();

    public ImportController(IImportService importService, IShortStringHelper shortStringHelper)
    {
        _importService = importService;
        _shortStringHelper = shortStringHelper;
    }

    public IActionResult ImportCategories(int depth, int quantityPerLevel)
    {
        var data = CreateFullDummyData(depth, quantityPerLevel);

        _importService.FullSync(data, rootCategory);

        return Ok();
    }

    private ImportData CreateFullDummyData(int depth, int quantityPerLevel)
    {
        ImportData data = new ImportData()
        {
            MediaRootKey = new Guid(),
            Categories = GenerateCategories(depth, quantityPerLevel, 1, ""),
            Products = products
        };

        return data;
    }

    private List<ImportCategory> GenerateCategories(int depth, int quantityPerLevel, int currentDepth, string parentIdentifier = null)
    {
        var categories = new List<ImportCategory>();

        // Expanded list of sample category names
        var sampleCategoryNames = new List<string>
        {
            "Electronics",
            "Computers",
            "Smartphones",
            "Accessories",
            "Laptops",
            "Tablets",
            "Cameras",
            "Headphones",
            "Speakers",
            "Smart Home Devices",
            "Wearable Technology",
            "Networking Devices",
            "Printers",
            "Scanners",
            "Monitors",
            "Video Games",
            "VR Equipment",
            "Drones",
            "Projectors",
            "External Storage"
        };

        for (int i = 0; i < quantityPerLevel; i++)
        {
            // Cycle through sampleCategoryNames to ensure variety
            var categoryNameIndex = (currentDepth - 1) * quantityPerLevel + i;
            categoryNameIndex = categoryNameIndex % sampleCategoryNames.Count;
            var categoryName = sampleCategoryNames[categoryNameIndex] + $" {currentDepth}-{i + 1}";

            var identifier = $"SKU-{currentDepth}-{i + 1}-{parentIdentifier}-{categoryName}";

            var category = new ImportCategory
            {
                Title = new Dictionary<string, object>
                {
                    { "en-US", $"{categoryName} US" },
                    { "is-IS", $"{categoryName} IS" }
                },
                Slug = new Dictionary<string, object>
                {
                    // Generates URL-friendly slug
                    { "en-US", ($"{categoryName} US").ToUrlSegment(_shortStringHelper).ToLowerInvariant() },
                    { "is-IS", ($"{categoryName} IS").ToUrlSegment(_shortStringHelper).ToLowerInvariant() }
                },
                SKU = identifier,
                NodeName = $"{categoryName}",
                Images = new List<IImportImage>()
                {
                     new ImportImageFromUdi()
                     {
                        ImageUdi = "udi://media/3b95537d28b24ce2b92e8d66c74c8fa5",
                         
                     }
                },
                SubCategories = currentDepth < depth
                    ? GenerateCategories(depth, quantityPerLevel, currentDepth + 1, $"{currentDepth}-{i + 1}")
                    : null, // No subcategories if it's the last level
            };

            categories.Add(category);

            if (currentDepth >= depth)
            {
                GenerateProducts(identifier, quantityPerLevel, currentDepth);
            }
        }

        return categories;
    }

    private List<ImportProduct> GenerateProducts(string identifier, int quantityPerLevel, int currentDepth)
    {
        for (int i = 0; i < quantityPerLevel; i++)
        {
            var product = new ImportProduct
            {
                Title = new Dictionary<string, object>
                {
                    { "en-US", $"Title {currentDepth} US {i + 1}" },
                    { "is-IS", $"Title {currentDepth} IS {i + 1}" }
                },
                Slug = new Dictionary<string, object>
                {
                    { "en-US", ($"Slug {currentDepth} US {i + 1}").ToUrlSegment(_shortStringHelper).ToLowerInvariant() },
                    { "is-IS", ($"Slug {currentDepth} IS {i + 1}").ToUrlSegment(_shortStringHelper).ToLowerInvariant() }
                },
                SKU = $"Product SKU {currentDepth}-{i + 1} - {identifier}",
                Description = new Dictionary<string, object>
                {
                    { "en-US", $"Description {currentDepth} US {i + 1}" },
                    { "is-IS", $"Description {currentDepth} IS {i + 1}" }
                },
                Categories = new List<string>()
                {
                    identifier,
                    "SKU-2-5-1-2-Printers 2-5"
                },
                Price = new List<ImportPrice>()
                {
                    new ImportPrice()
                    {
                        StoreAlias = "Store",
                        Currency = "is-IS",
                        Price = 1500
                    },
                    new ImportPrice()
                    {
                        StoreAlias = "Store2",
                        Currency = "is-IS",
                        Price = 2500
                    }
                },
                Stock = new List<ImportStock>()
                {
                    new ImportStock()
                    {
                        Stock = 3,
                        StoreAlias = ""
                    }
                },
                NodeName = $"Product {currentDepth}-{i + 1}",
                AdditionalProperties = new Dictionary<string, object>
                {
                    { "customPropertyFilter", $"customProperty-{currentDepth}-{i + 1}" }
                }
            };

            products.Add(product);
        }

        return products;
    }
}
