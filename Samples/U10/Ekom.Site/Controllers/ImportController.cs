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

    private readonly Guid rootCategory = new Guid("c169268a-af96-4c47-a35f-5cbd03dff8c8");
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
            MediaRootKey = new Guid("4dc98622-a146-4010-a758-d8a995fd0b08"),
            Categories = NewCategories(),//GenerateCategories(depth, quantityPerLevel, 1, "", ""),
            Products = products
        };

        return data;
    }

    private List<ImportCategory> NewCategories()
    {
        var tree = new List<ImportCategory>
        {
            new ImportCategory
            {
                Title = new Dictionary<string, object> {
                    { "en-US", $"1US" },
                    { "is-IS", $"1IS" }
                },
                SKU = "1",
                Identifier = "1",
                ParentIdentifier = "",
                NodeName = $"1",
                AdditionalProperties = new Dictionary<string, object>()
                {
                    { "updateSlug", true }
                },
                SubCategories = new List<ImportCategory>() {
                    { 
                        new ImportCategory()
                        {
                            Identifier = "11",
                            NodeName = "11",
                            ParentIdentifier = "1",
                            AdditionalProperties = new Dictionary<string, object>()
                            {
                                { "updateSlug", true }
                            },
                            Title= new Dictionary<string, object> {
                                { "en-US", $"11US" },
                                { "is-IS", $"11IS" }
                            }
                        } 
                    },
                    {
                        new ImportCategory()
                        {
                            Identifier = "12",
                            NodeName = "12",
                            ParentIdentifier = "1",
                            AdditionalProperties = new Dictionary<string, object>()
                            {
                                { "updateSlug", true }
                            },
                            Title= new Dictionary<string, object> {
                                { "en-US", $"12US" },
                                { "is-IS", $"12IS" }
                            }
                        }
                    }
                }
            },
            new ImportCategory
            {
                Title = new Dictionary<string, object> {
                    { "en-US", $"2US" },
                    { "is-IS", $"2IS" }
                },
                SKU = "2",
                Identifier = "2",
                ParentIdentifier = "",
                NodeName = $"2",
                AdditionalProperties = new Dictionary<string, object>()
                {
                    { "updateSlug", true }
                },
                SubCategories = new List<ImportCategory>() {
                    {
                        new ImportCategory()
                        {
                            Identifier = "21",
                            NodeName = "21",
                            ParentIdentifier = "2",
                            Title= new Dictionary<string, object> {
                                { "en-US", $"21US" },
                                { "is-IS", $"21IS" }
                            },
                            AdditionalProperties = new Dictionary<string, object>()
                            {
                                { "updateSlug", true }
                            },
                        }
                    },
                    {
                        new ImportCategory()
                        {
                            Identifier = "22",
                            NodeName = "22",
                            ParentIdentifier = "2",
                            Title= new Dictionary<string, object> {
                                { "en-US", $"22US" },
                                { "is-IS", $"22IS" }
                            },
                            AdditionalProperties = new Dictionary<string, object>()
                            {
                                { "updateSlug", true }
                            },
                        }
                    }
                }
            }
        };

        GenerateProducts("", 2, 1);

        return tree;
    }


    private List<ImportCategory> GenerateCategories(int depth, int quantityPerLevel, int currentDepth, string? parentIdentifier = null, string? fullParentIdentifier = null)
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
                Identifier = identifier,
                ParentIdentifier = fullParentIdentifier ?? "",
                NodeName = $"{categoryName}",
                Images = new List<IImportMedia>()
                {
                    new ImportMediaFromUdi()
                    {
                        Udi = "udi://media/3b95537d28b24ce2b92e8d66c74c8fa5",
                    },
                    new ImportMediaFromExternalUrl()
                    {
                        FileName = "testCategory.jpg",
                        NodeName = "Test Category nodename",
                        Url = "https://www.vettvangur.is/images/illustrations/2.png"
                    }
                },
                SubCategories = currentDepth < depth
                    ? GenerateCategories(depth, quantityPerLevel, currentDepth + 1, $"{currentDepth}-{i + 1}", identifier)
                    : new List<ImportCategory>(), // No subcategories if it's the last level
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
                NodeName = $"Product {currentDepth}-{i + 1}",
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
                Identifier = $"Product SKU {currentDepth}-{i + 1} - {identifier}",
                Description = new Dictionary<string, object>
                {
                    { "en-US", $"Description {currentDepth} US {i + 1}" },
                    { "is-IS", $"Description {currentDepth} IS {i + 1}" }
                },
                Categories = new List<string>()
                {
                    "21"
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
                Images = new List<IImportMedia>()
                {
                    new ImportMediaFromUdi()
                    {
                        Udi = "udi://media/3b95537d28b24ce2b92e8d66c74c8fa5",
                    },
                    new ImportMediaFromExternalUrl()
                    {
                        FileName = "test.jpg",
                        NodeName = "Test nodename",
                        Url = "https://www.vettvangur.is/images/illustrations/1.png"
                    }
                },
                VariantGroups = new List<ImportVariantGroup>()
                {
                    new ImportVariantGroup
                    {
                        Title = new Dictionary<string, object>
                        {
                            { "en-US", $"Variants" },
                            { "is-IS", $"Variants" }
                        },
                        NodeName = "Variants",
                        Identifier = $"Variant Group SKU {currentDepth}-{i + 1} - {identifier}",
                        SaveEvent = ImportSaveEntEnum.SavePublish,
                        Variants = new List<ImportVariant>()
                        {
                            new ImportVariant()
                            {
                                Title = new Dictionary<string, object>
                                {
                                    { "en-US", $"Variant" },
                                    { "is-IS", $"Variant" }
                                },
                                NodeName = "Variant",
                                Identifier = $"Variant SKU {currentDepth}-{i + 1} - {identifier}",
                                AdditionalProperties =  new Dictionary<string, object>
                                {
                                    { "discount", $"1234" },
                                }
                            }
                        }
                    }
                },
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
