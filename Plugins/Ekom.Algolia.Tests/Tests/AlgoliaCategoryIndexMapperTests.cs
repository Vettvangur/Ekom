using Ekom.Algolia;
using Ekom.Algolia.Mappers;
using Moq;
using Xunit;

namespace Ekom.Algolia.Tests.Tests;

public class AlgoliaCategoryIndexMapperTests
{
    [Fact]
    public void Maps_Category_Record_With_Hierarchy_And_Path()
    {
        var mapper = CreateMapper();
        var category = CreateCategory(
            title: "Chocolate",
            nodeName: "Chocolate Node",
            slug: "chocolate",
            ancestors: [CreateCategory("Candy")]);

        var record = mapper.Map(category.Object, CreateStore("en-US"), "categories");

        Assert.NotNull(record);
        Assert.Equal("Chocolate Node", record!.NodeName);
        Assert.Equal("Chocolate", record.Title);
        Assert.Equal("chocolate", record.Slug);
        Assert.Equal("/candy/chocolate", record.Url);
        Assert.Equal("Candy", record.Data["hierarchical_categories.lvl0"]);
        Assert.Equal("Candy > Chocolate", record.Data["hierarchical_categories.lvl1"]);
        Assert.Equal("Candy > Chocolate", record.Data["category_path"]);
    }

    [Fact]
    public void Uses_Localized_Title_When_Available()
    {
        var mapper = CreateMapper();
        var category = CreateCategory(
            title: "Chocolate",
            nodeName: "Chocolate Node",
            localizedTitle: "Súkkulaði");

        var record = mapper.Map(category.Object, CreateStore("is-IS"), "categories");

        Assert.NotNull(record);
        Assert.Equal("Chocolate Node", record!.NodeName);
        Assert.Equal("Súkkulaði", record.Title);
        Assert.Equal("Súkkulaði", record.Data["category_path"]);
    }

    private static CategoryIndexMapper CreateMapper()
        => new();

    private static AlgoliaResolvedStore CreateStore(string? locale = null) => new()
    {
        Alias = "store",
        Locale = locale
    };

    private static Mock<Ekom.Models.ICategory> CreateCategory(
        string title,
        string? nodeName = null,
        string slug = "slug",
        string? localizedTitle = null,
        IReadOnlyList<Mock<Ekom.Models.ICategory>>? ancestors = null)
    {
        var category = new Mock<Ekom.Models.ICategory>();
        category.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        category.SetupGet(x => x.Title).Returns(title);
        category.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nodeName"] = nodeName ?? title
        });
        category.SetupGet(x => x.Slug).Returns(slug);
        category.SetupGet(x => x.Url).Returns($"/{slug}");
        category.SetupGet(x => x.Urls).Returns([$"/{slug}"]);
        category.SetupGet(x => x.UrlsWithContext).Returns([
            new Ekom.Models.Umbraco.UmbracoUrl
            {
                Url = $"/{slug}",
                Culture = string.Empty
            },
            new Ekom.Models.Umbraco.UmbracoUrl
            {
                Url = $"/candy/{slug}",
                Culture = "en-US"
            }
        ]);
        category.SetupGet(x => x.ParentId).Returns(42);
        category.SetupGet(x => x.SortOrder).Returns(3);
        category.SetupGet(x => x.CreateDate).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        category.SetupGet(x => x.UpdateDate).Returns(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        category.SetupGet(x => x.Ancestors).Returns((ancestors ?? []).Select(x => x.Object));
        category.Setup(x => x.GetValue("nodeName", It.IsAny<string?>(), It.IsAny<bool>())).Returns(nodeName ?? title);
        category.Setup(x => x.GetValue("title", It.IsAny<string?>(), true))
            .Returns((string _, string? locale, bool _) =>
                string.Equals(locale, "is-IS", StringComparison.OrdinalIgnoreCase) ? localizedTitle ?? string.Empty : string.Empty);

        return category;
    }
}
