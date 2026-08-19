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
        var candyKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var chocolateKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var organicKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories:
            [
                CreateCategory("Chocolate", ancestors: [CreateCategory("Candy", key: candyKey)], key: chocolateKey),
                CreateCategory("Organic", key: organicKey)
            ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(["Candy", "Organic"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record!.Data["hierarchical_categories.lvl0"]));
        Assert.Equal(["Candy > Chocolate"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record.Data["hierarchical_categories.lvl1"]));
        Assert.Equal(
            ["Candy", "Candy > Chocolate", "Organic"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(record.Data["category_paths"]));
        Assert.Equal(
            [candyKey.ToString("D"), chocolateKey.ToString("D"), organicKey.ToString("D")],
            record.CategoryPageId);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(record));
        Assert.True(json.RootElement.TryGetProperty("categoryPageId", out _));
        Assert.False(json.RootElement.TryGetProperty("CategoryPageId", out _));
    }

    [Fact]
    public void Deduplicates_Category_Page_Ids_While_Preserving_Order()
    {
        var rootKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var firstKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var secondKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var root = CreateCategory("Root", key: rootKey);
        var mapper = CreateMapper();
        var product = CreateProduct(
            categories:
            [
                CreateCategory("First", ancestors: [root], key: firstKey),
                CreateCategory("Second", ancestors: [root], key: secondKey)
            ]);

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal(
            [rootKey.ToString("D"), firstKey.ToString("D"), secondKey.ToString("D")],
            record!.CategoryPageId);
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
    public void Strips_Html_From_Configured_Product_Property()
    {
        var mapper = CreateMapper(productProperties: ["body|striphtml"]);
        var product = CreateProduct(properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["body"] = "<p>Hello&nbsp;<strong>world</strong></p><p>Next</p>"
        });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal("Hello world Next", Assert.IsType<string>(record!.Data["body"]));
    }

    [Fact]
    public void Strips_Html_From_Built_In_Product_Text_Fields()
    {
        var mapper = CreateMapper(productProperties: ["TITLE|STRIPHTML", "summary|striphtml", "description|striphtml"]);
        var product = CreateProduct(
            title: "<p>Product <strong>title</strong></p>",
            summary: "<div>Product&nbsp;summary</div>",
            description: "{\"markup\":\"<p>Product description</p>\"}");

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Equal("Product title", record!.Title);
        Assert.Equal("Product summary", record.Summary);
        Assert.Equal("Product description", record.Description);
        Assert.DoesNotContain(record.Data.Keys, key => key.Equals("title", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Data.Keys, key => key.Equals("summary", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Data.Keys, key => key.Equals("description", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Omits_Configured_Values_That_Are_Empty_After_Stripping_Html()
    {
        var mapper = CreateMapper(productProperties: ["body|striphtml", "summary|striphtml"]);
        var product = CreateProduct(
            summary: "<script>alert('ignored')</script>",
            properties: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["body"] = "<style>.hidden { display: none; }</style>"
            });

        var record = mapper.Map(product.Object, CreateStore(), "products");

        Assert.NotNull(record);
        Assert.Null(record!.Summary);
        Assert.DoesNotContain("body", record.Data.Keys);
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
        Assert.Null(record.CategoryPageId);
        Assert.DoesNotContain("emptyProp", record.Data.Keys);
        Assert.DoesNotContain("blankProp", record.Data.Keys);
        Assert.False(Assert.IsType<bool>(record.Data["featured"]));

        var json = JsonSerializer.Serialize(record);

        Assert.DoesNotContain("Summary", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Description", json, StringComparison.Ordinal);
        Assert.DoesNotContain("image_url", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageUrls", json, StringComparison.Ordinal);
        Assert.DoesNotContain("categoryPageId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("emptyProp", json, StringComparison.Ordinal);
        Assert.DoesNotContain("blankProp", json, StringComparison.Ordinal);
        Assert.Contains("\"featured\":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRecords_Returns_Only_Product_When_Variant_Indexing_Is_Disabled()
    {
        var mapper = CreateMapper(indexVariants: false);
        var product = CreateProduct(variants: [CreateVariant().Object]);

        var records = mapper.MapRecords(product.Object, CreateStore(), "products");

        Assert.Single(records);
        Assert.False(records[0].IsVariant);
        Assert.Equal(product.Object.Key.ToString(), records[0].ProductId);
    }

    [Fact]
    public void MapRecords_Maps_Variants_When_Variant_Indexing_Is_Enabled()
    {
        var mapper = CreateMapper(indexVariants: true);
        var variant = CreateVariant(sku: "variant-sku", title: "Variant title");
        var categoryKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var product = CreateProduct(
            categories: [CreateCategory("Category", key: categoryKey)],
            sku: "placeholder",
            variants: [variant.Object]);

        var records = mapper.MapRecords(product.Object, CreateStore(), "products");

        Assert.Equal(2, records.Count);
        var variantRecord = records.Single(x => x.IsVariant);

        Assert.Equal($"{product.Object.Key}_{variant.Object.Key}", variantRecord.ObjectId);
        Assert.Equal(product.Object.Key.ToString(), variantRecord.ProductId);
        Assert.Equal(variant.Object.Key.ToString(), variantRecord.VariantId);
        Assert.Equal("variant-sku", variantRecord.Sku);
        Assert.Equal("placeholder", variantRecord.ParentSku);
        Assert.Equal([categoryKey.ToString("D")], variantRecord.CategoryPageId);
        Assert.Equal("variant-sku", Assert.IsType<string>(variantRecord.Data["variantSku"]));
        Assert.Equal("Variant title", Assert.IsType<string>(variantRecord.Data["variantTitle"]));
    }

    [Fact]
    public void MapRecords_Strips_Html_From_Variant_Description()
    {
        var mapper = CreateMapper(productProperties: ["description|striphtml"], indexVariants: true);
        var variant = CreateVariant(description: "<p>Variant <strong>description</strong></p>");
        var product = CreateProduct(variants: [variant.Object]);

        var records = mapper.MapRecords(product.Object, CreateStore(), "products");

        var variantRecord = records.Single(x => x.IsVariant);
        Assert.Equal("Variant description", variantRecord.Description);
        Assert.Equal("Variant description", Assert.IsType<string>(variantRecord.Data["variantDescription"]));
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
    public void Strips_Html_From_Configured_Metafield()
    {
        var mapper = CreateMapper(productProperties: ["metafield:longDescription|striphtml"]);
        var product = CreateProduct(metafields:
        [
            CreateMetafield("longDescription", [CreateMetafieldValue(("is-IS", "{\"markup\":\"<p>Halló <strong>heimur</strong></p>\"}"))])
        ]);

        var record = mapper.Map(product.Object, CreateStore(locale: "is-IS"), "products");

        Assert.NotNull(record);
        Assert.Equal("Halló heimur", Assert.IsType<string>(record!.Data["longDescription"]));
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

    private static ProductIndexMapper CreateMapper(IReadOnlyCollection<string>? productProperties = null, bool indexVariants = false)
    {
        var options = Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Indexing = new AlgoliaIndexingOptions
            {
                Variants = indexVariants,
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
        string? algoliaRank = null,
        Guid? key = null)
    {
        var category = new Mock<Ekom.Models.ICategory>();
        category.SetupGet(x => x.Key).Returns(key ?? Guid.Empty);
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
        bool available = true,
        IReadOnlyList<Ekom.Models.IVariant>? variants = null)
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
        product.SetupGet(x => x.AllVariants).Returns(variants ?? []);
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

    private static Mock<Ekom.Models.IVariant> CreateVariant(
        string sku = "variant-sku",
        string title = "Variant",
        string description = "Variant description",
        bool available = true)
    {
        var price = new Mock<Ekom.Models.IPrice>();
        price.SetupGet(x => x.Value).Returns(10m);
        price.SetupGet(x => x.WithVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 10m));
        price.SetupGet(x => x.WithoutVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 8m));
        price.SetupGet(x => x.Currency).Returns(new Ekom.Models.CurrencyModel { CurrencyValue = "ISK", CurrencyFormat = "0" });

        var variant = new Mock<Ekom.Models.IVariant>();
        variant.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        variant.SetupGet(x => x.SKU).Returns(sku);
        variant.SetupGet(x => x.Title).Returns(title);
        variant.SetupGet(x => x.Description).Returns(description);
        variant.SetupGet(x => x.Images).Returns([]);
        variant.SetupGet(x => x.Available).Returns(available);
        variant.SetupGet(x => x.Stock).Returns(5m);
        variant.SetupGet(x => x.Price).Returns(price.Object);
        variant.SetupGet(x => x.Prices).Returns([price.Object]);
        variant.SetupGet(x => x.VariantGroupId).Returns(123);
        variant.SetupGet(x => x.CreateDate).Returns(new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        variant.SetupGet(x => x.UpdateDate).Returns(new DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc));

        return variant;
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
