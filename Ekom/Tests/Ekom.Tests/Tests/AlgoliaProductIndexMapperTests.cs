using Ekom.Algolia;
using Ekom.Algolia.Mappers;
using Ekom.Models.Umbraco;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaProductIndexMapperTests
{
    [Fact]
    public void Maps_Category_Levels_For_Hierarchical_Menu()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories:
            [
                CreateCategory("Chocolate", ancestors: [CreateCategory("Candy")]),
                CreateCategory("Organic")
            ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(["Candy", "Organic"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record!.Data["hierarchical_categories.lvl0"]));
        Assert.Equal(["Candy > Chocolate"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record.Data["hierarchical_categories.lvl1"]));
        Assert.Equal(
            ["Candy", "Candy > Chocolate", "Organic"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(record.Data["category_paths"]));
    }

    [Fact]
    public void Maps_NodeName_From_Umbraco_Node_Name_Not_Title()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(
            title: "Display Product Title",
            nodeName: "Actual Umbraco Node Name");

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal("Actual Umbraco Node Name", record!.NodeName);
        Assert.Equal("Display Product Title", record.Title);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Maps_Availability_As_Numeric_Ranking_Value(bool available, int expected)
    {
        var mapper = CreateMapper();
        var product = CreateProduct(available: available);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(expected, record!.Available);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void Maps_Boolean_Product_Properties_From_Umbraco_Flags(string rawValue, bool expected)
    {
        var mapper = CreateMapper(productProperties: ["featured"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["featured"] = rawValue
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(expected, Assert.IsType<bool>(record!.Data["featured"]));
    }

    [Fact]
    public void Maps_Int_Product_Properties_From_Configured_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["stockCount|int"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stockCount"] = "0"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(0, Assert.IsType<int>(record!.Data["stockCount"]));
    }

    [Theory]
    [InlineData("0,1", 0.1)]
    [InlineData("0.0", 0.0)]
    public void Maps_Decimal_Product_Properties_From_Configured_Modifier(string rawValue, decimal expected)
    {
        var mapper = CreateMapper(productProperties: ["weight|decimal"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weight"] = rawValue
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(expected, Assert.IsType<decimal>(record!.Data["weight"]));
    }

    [Fact]
    public void Maps_Array_Product_Properties_From_Configured_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["channels|array"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["channels"] = "[\"Web\", \"Store\", \"Web\", \" \" ]"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(["Web", "Store"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record!.Data["channels"]));
    }

    [Fact]
    public void Skips_Invalid_Int_Product_Properties_From_Configured_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["stockCount|int"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stockCount"] = "abc"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("stockCount", record!.Data.Keys);
    }

    [Fact]
    public void Skips_Invalid_Decimal_Product_Properties_From_Configured_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["weight|decimal"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weight"] = "abc"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("weight", record!.Data.Keys);
    }

    [Fact]
    public void Skips_Invalid_Array_Product_Properties_From_Configured_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["channels|array"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["channels"] = "Web"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("channels", record!.Data.Keys);
    }

    [Fact]
    public void Omits_Empty_And_Whitespace_Values_From_Indexed_Record()
    {
        var mapper = CreateMapper(productProperties: ["emptyProp", "blankProp", "featured"]);
        var product = CreateProduct(
            summary: "   ",
            description: string.Empty,
            sku: " ",
            url: " ",
            properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["emptyProp"] = string.Empty,
                ["blankProp"] = "   ",
                ["featured"] = "0"
            });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Null(record!.Sku);
        Assert.Null(record.Summary);
        Assert.Null(record.Description);
        Assert.Null(record.Url);
        Assert.Null(record.ImageUrl);
        Assert.Null(record.ImageUrls);
        Assert.DoesNotContain("emptyProp", record.Data.Keys);
        Assert.DoesNotContain("blankProp", record.Data.Keys);
        Assert.False(Assert.IsType<bool>(record.Data["featured"]));

        var json = JsonSerializer.Serialize(record);

        Assert.DoesNotContain("Summary", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Description", json, StringComparison.Ordinal);
        Assert.DoesNotContain("image_url", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageUrls", json, StringComparison.Ordinal);
        Assert.DoesNotContain("emptyProp", json, StringComparison.Ordinal);
        Assert.DoesNotContain("blankProp", json, StringComparison.Ordinal);
        Assert.Contains("\"featured\":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_Relative_Urls_And_Image_Urls_As_Is()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(url: "/product/fallback");
        product.SetupGet(x => x.Images).Returns([
            new Ekom.Models.Image { Url = "/media/product.jpg" }
        ]);
        product.SetupGet(x => x.UrlsWithContext).Returns([
            new UmbracoUrl { Culture = "is-IS", Url = "/product/context" }
        ]);

        var record = mapper.Map(product.Object, CreateStore(locale: "is-IS"), "products");

        Assert.NotNull(record);
        Assert.Equal("/product/context", record!.Url);
        Assert.Equal("/media/product.jpg", record.ImageUrl);
        Assert.Equal(["/media/product.jpg"], record.ImageUrls);
    }

    [Fact]
    public void Maps_Scalar_Metafield_When_Explicitly_Configured()
    {
        var mapper = CreateMapper(productProperties: ["metafield:material"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("material", [CreateMetafieldValue((string.Empty, "Cotton"))])
        ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal("Cotton", Assert.IsType<string>(record!.Data["material"]));
    }

    [Fact]
    public void Maps_Localized_Metafield_When_Explicitly_Configured()
    {
        var mapper = CreateMapper(productProperties: ["metafield:color"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("color", [CreateMetafieldValue(("en-US", "Red"), ("is-IS", "Rauður"))])
        ]);

        var record = mapper.Map(product.Object, CreateStore(locale: "is-IS"), "products");

        Assert.NotNull(record);
        Assert.Equal("Rauður", Assert.IsType<string>(record!.Data["color"]));
    }

    [Fact]
    public void Maps_Multi_Value_Metafield_As_Array_When_Configured()
    {
        var mapper = CreateMapper(productProperties: ["metafield:channels|array"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("channels",
            [
                CreateMetafieldValue((string.Empty, "Web")),
                CreateMetafieldValue((string.Empty, "Store")),
                CreateMetafieldValue((string.Empty, "Web"))
            ])
        ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(["Web", "Store"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record!.Data["channels"]));
    }

    [Fact]
    public void Skips_Multi_Value_Metafield_Without_Array_Modifier()
    {
        var mapper = CreateMapper(productProperties: ["metafield:channels"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("channels",
            [
                CreateMetafieldValue((string.Empty, "Web")),
                CreateMetafieldValue((string.Empty, "Store"))
            ])
        ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("channels", record!.Data.Keys);
    }

    [Fact]
    public void Skips_Invalid_Int_Metafield_Value()
    {
        var mapper = CreateMapper(productProperties: ["metafield:stockLevel|int"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("stockLevel", [CreateMetafieldValue((string.Empty, "abc"))])
        ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("stockLevel", record!.Data.Keys);
    }

    [Fact]
    public void Plain_Property_Alias_Does_Not_Read_Metafield_Value()
    {
        var mapper = CreateMapper(productProperties: ["material"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("material", [CreateMetafieldValue((string.Empty, "Cotton"))])
        ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.DoesNotContain("material", record!.Data.Keys);
    }

    [Fact]
    public void Maps_Product_And_Highest_Category_Ranking()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories:
            [
                CreateCategory("Primary", algoliaRank: "5"),
                CreateCategory("Secondary", algoliaRank: "9"),
                CreateCategory("Third", algoliaRank: "2")
            ],
            properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ekmAlgoliaRank"] = "12"
            });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(12, record!.ProductRanking);
        Assert.Equal(9, record.CategoryRanking);
    }

    [Fact]
    public void Allows_Negative_Product_And_Category_Ranking()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories:
            [
                CreateCategory("Primary", algoliaRank: "-10"),
                CreateCategory("Secondary")
            ],
            properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ekmAlgoliaRank"] = "-4"
            });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(-4, record!.ProductRanking);
        Assert.Equal(-10, record.CategoryRanking);
    }

    [Fact]
    public void Defaults_Product_And_Category_Ranking_To_Zero_When_Missing_Or_Invalid()
    {
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories: [CreateCategory("Primary", algoliaRank: "invalid")],
            properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ekmAlgoliaRank"] = "invalid"
            });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(0, record!.ProductRanking);
        Assert.Equal(0, record.CategoryRanking);
    }

    private static ProductIndexMapper CreateMapper(IReadOnlyCollection<string>? productProperties = null)
    {
        var options = Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Indexing = new AlgoliaIndexingOptions
            {
                ProductProperties = productProperties ?? []
            }
        });

        return new ProductIndexMapper(options);
    }

    private static AlgoliaResolvedStore CreateStore(string? locale = null) => new()
    {
        Alias = "store",
        Locale = locale
    };

    private static Mock<Ekom.Models.ICategory> CreateCategory(
        string title,
        IReadOnlyList<Mock<Ekom.Models.ICategory>>? ancestors = null,
        string? algoliaRank = null)
    {
        var category = new Mock<Ekom.Models.ICategory>();
        category.SetupGet(x => x.Title).Returns(title);
        category.SetupGet(x => x.Ancestors).Returns((ancestors ?? []).Select(x => x.Object));
        category.Setup(x => x.GetValue("title", It.IsAny<string?>(), true)).Returns(string.Empty);
        category.Setup(x => x.GetValue("ekmAlgoliaRank", It.IsAny<string?>(), false)).Returns(algoliaRank ?? string.Empty);

        return category;
    }

    private static Mock<Ekom.Models.IProduct> CreateProduct(
        IReadOnlyList<Mock<Ekom.Models.ICategory>>? categories = null,
        IReadOnlyList<Ekom.Models.MetavalueSlim>? metafields = null,
        IReadOnlyDictionary<string, string>? properties = null,
        string title = "Product",
        string nodeName = "Product Node",
        string sku = "sku",
        string summary = "Summary",
        string description = "Description",
        string url = "/product",
        bool available = true)
    {
        var price = new Mock<Ekom.Models.IPrice>();
        price.SetupGet(x => x.Value).Returns(100m);
        price.SetupGet(x => x.WithVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 100m));
        price.SetupGet(x => x.WithoutVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 80m));
        price.SetupGet(x => x.Currency).Returns(new Ekom.Models.CurrencyModel { CurrencyValue = "ISK", CurrencyFormat = "0" });

        var product = new Mock<Ekom.Models.IProduct>();
        product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        product.SetupGet(x => x.SKU).Returns(sku);
        product.SetupGet(x => x.Title).Returns(title);
        product.SetupGet(x => x.Summary).Returns(summary);
        product.SetupGet(x => x.Description).Returns(description);
        product.SetupGet(x => x.Url).Returns(url);
        product.SetupGet(x => x.Images).Returns([]);
        product.SetupGet(x => x.UrlsWithContext).Returns([]);
        product.SetupGet(x => x.Urls).Returns([]);
        product.SetupGet(x => x.Available).Returns(available);
        product.SetupGet(x => x.Stock).Returns(10m);
        product.SetupGet(x => x.Price).Returns(price.Object);
        product.SetupGet(x => x.Prices).Returns([price.Object]);
        product.SetupGet(x => x.Categories).Returns((categories ?? []).Select(x => x.Object));
        product.SetupGet(x => x.Metafields).Returns(metafields?.ToList() ?? []);
        product.SetupGet(x => x.CreateDate).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        product.SetupGet(x => x.UpdateDate).Returns(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nodeName"] = nodeName
        });
        product.Setup(x => x.GetValue("title", It.IsAny<string?>(), true)).Returns(string.Empty);
        product.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<string?>(), false))
            .Returns((string alias, string? _, bool _) => properties != null && properties.TryGetValue(alias, out var value) ? value : string.Empty);
        product.Setup(x => x.GetValue("nodeName", It.IsAny<string?>(), It.IsAny<bool>())).Returns(nodeName);

        return product;
    }

    private static Ekom.Models.MetavalueSlim CreateMetafield(string alias, IReadOnlyList<Dictionary<string, string>> values)
        => new()
        {
            Field = new Ekom.Models.MetafieldSlim
            {
                Alias = alias,
                Name = alias,
                Description = string.Empty
            },
            Values = values.ToList()
        };

    private static Dictionary<string, string> CreateMetafieldValue(params (string Key, string Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
}
