using Ekom.Algolia;
using Ekom.Algolia.Mappers;
using Microsoft.Extensions.Options;
using Moq;
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
        Assert.Equal(["Candy", "Organic"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record!.Data["categories.lvl0"]));
        Assert.Equal(["Candy > Chocolate"], Assert.IsAssignableFrom<IReadOnlyList<string>>(record.Data["categories.lvl1"]));
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
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var price = new Mock<Ekom.Models.IPrice>();
        price.SetupGet(x => x.Value).Returns(100m);
        price.SetupGet(x => x.WithVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 100m));
        price.SetupGet(x => x.WithoutVat).Returns(Mock.Of<Ekom.Models.ICalculatedPrice>(p => p.Value == 80m));
        price.SetupGet(x => x.Currency).Returns(new Ekom.Models.CurrencyModel { CurrencyValue = "ISK", CurrencyFormat = "0" });

        var product = new Mock<Ekom.Models.IProduct>();
        product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        product.SetupGet(x => x.SKU).Returns("sku");
        product.SetupGet(x => x.Title).Returns("Product");
        product.SetupGet(x => x.Summary).Returns("Summary");
        product.SetupGet(x => x.Description).Returns("Description");
        product.SetupGet(x => x.Url).Returns("/product");
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
