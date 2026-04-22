using Ekom.Algolia;
using Ekom.Algolia.Mappers;
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

    private static AlgoliaResolvedStore CreateStore() => new()
    {
        Alias = "store"
    };

    private static Mock<Ekom.Models.ICategory> CreateCategory(string title, IReadOnlyList<Mock<Ekom.Models.ICategory>>? ancestors = null)
    {
        var category = new Mock<Ekom.Models.ICategory>();
        category.SetupGet(x => x.Title).Returns(title);
        category.SetupGet(x => x.Ancestors).Returns((ancestors ?? []).Select(x => x.Object));
        category.Setup(x => x.GetValue("title", It.IsAny<string?>(), true)).Returns(string.Empty);

        return category;
    }

    private static Mock<Ekom.Models.IProduct> CreateProduct(
        IReadOnlyList<Mock<Ekom.Models.ICategory>>? categories = null,
        IReadOnlyDictionary<string, string>? properties = null,
        string sku = "sku",
        string summary = "Summary",
        string description = "Description",
        string url = "/product")
    {
        var price = new Mock<Ekom.Models.IPrice>();
        price.SetupGet(x => x.Value).Returns(100m);
        price.SetupGet(x => x.WithVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 100m));
        price.SetupGet(x => x.WithoutVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 80m));
        price.SetupGet(x => x.Currency).Returns(new Ekom.Models.CurrencyModel { CurrencyValue = "ISK", CurrencyFormat = "0" });

        var product = new Mock<Ekom.Models.IProduct>();
        product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        product.SetupGet(x => x.SKU).Returns(sku);
        product.SetupGet(x => x.Title).Returns("Product");
        product.SetupGet(x => x.Summary).Returns(summary);
        product.SetupGet(x => x.Description).Returns(description);
        product.SetupGet(x => x.Url).Returns(url);
        product.SetupGet(x => x.Images).Returns([]);
        product.SetupGet(x => x.UrlsWithContext).Returns([]);
        product.SetupGet(x => x.Urls).Returns([]);
        product.SetupGet(x => x.Available).Returns(true);
        product.SetupGet(x => x.Stock).Returns(10m);
        product.SetupGet(x => x.Price).Returns(price.Object);
        product.SetupGet(x => x.Prices).Returns([price.Object]);
        product.SetupGet(x => x.Categories).Returns((categories ?? []).Select(x => x.Object));
        product.SetupGet(x => x.CreateDate).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        product.SetupGet(x => x.UpdateDate).Returns(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        product.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<string?>(), true)).Returns(string.Empty);
        product.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<string?>(), false))
            .Returns((string alias, string? _, bool _) => properties != null && properties.TryGetValue(alias, out var value) ? value : string.Empty);

        return product;
    }
}
