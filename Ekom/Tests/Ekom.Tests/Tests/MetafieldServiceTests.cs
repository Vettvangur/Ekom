using Ekom.Models;
using Ekom.Models.Comparers;
using Ekom.Services;
using Ekom.Tests.Objects;
using EkomCore.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Ekom.Tests.Tests;

// ProductQuery reads the shared test configuration, so do not run beside other configuration scopes.
[Collection(ProductFilterStoreContextCollection.Name)]
public class MetafieldServiceTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Filters_MatchTheOriginalLinqImplementation(int seed, bool filterable)
    {
        var products = CreateProducts(seed, 400);
        var service = CreateService(new Mock<INodeService>().Object);

        var expected = ReferenceFilters(products, filterable).ToList();
        var actual = service.Filters(products, filterable).ToList();

        Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));
        Assert.NotEmpty(actual);
    }

    [Fact]
    public void Filters_AreDeferredAndHandleProductsWithoutMetafields()
    {
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Metafields).Returns([]);
        var service = CreateService(new Mock<INodeService>().Object);

        var filters = service.Filters([product.Object]);
        product.VerifyGet(x => x.Metafields, Times.Never);

        Assert.Empty(filters);
        product.VerifyGet(x => x.Metafields, Times.Once);
    }

    [Theory]
    [InlineData("color", new[] { "Red" }, new[] { 1, 3 })]
    [InlineData("COLOR", new[] { "Red", "Blue" }, new[] { 1, 2, 3 })]
    [InlineData("11", new[] { "Blue" }, new[] { 2, 3 })]
    [InlineData("011", new[] { "Blue" }, new int[0])]
    [InlineData("features", new[] { "Waterproof", "Recycled" }, new[] { 3 })]
    [InlineData("features", new[] { "Waterproof" }, new[] { 1, 3 })]
    [InlineData("missing", new[] { "Red" }, new int[0])]
    public void FilterProducts_MatchesByAliasOrIdWithAnyOrAllSemantics(string key, string[] values, int[] expectedIds)
    {
        using var configuration = new ConfigurationScope();
        var color = Field(11, "color", allMustMatch: false);
        var features = Field(12, "features", allMustMatch: true);
        var products = new[]
        {
            Product(1, (color, ["Red"]), (features, ["Waterproof"])),
            Product(2, (color, ["Blue"])),
            Product(3, (color, ["Red", "Blue"]), (features, ["Waterproof", "Recycled"])),
            Product(4),
        };
        var service = CreateService(new Mock<INodeService>().Object);
        var query = new ProductQuery { MetaFilters = new() { [key] = values.ToList() } };

        var result = service.FilterProducts(products, query).Select(x => x.Id).ToArray();

        Assert.Equal(expectedIds, result);
    }

    [Fact]
    public void GetMetafields_MapsDefinitionsOnceAndReusesThem()
    {
        var nodes = new Mock<INodeService>();
        nodes.Setup(x => x.NodesByTypes("ekmMetafield")).Returns(() => new[] { MetafieldNode("Color"), MetafieldNode("Size") });
        var service = CreateService(nodes.Object);

        var first = service.GetMetafields().ToList();
        var second = service.GetMetafields().ToList();

        Assert.Equal(2, first.Count);
        Assert.Same(first[0], second[0]);
        Assert.Same(first[1], second[1]);
        nodes.Verify(x => x.NodesByTypes("ekmMetafield"), Times.Once);
    }

    private const string ColorKey = "11111111-1111-1111-1111-111111111111";
    private const string TextKey = "22222222-2222-2222-2222-222222222222";

    public static IEnumerable<object[]> MetafieldJson()
    {
        string[] values =
        [
            "[]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":\"a\"},{\"id\":\"b\"}]}]",
            "[{\"Key\":\"" + ColorKey + "\",\"Values\":[{\"Id\":\"a\",\"Values\":{\"en-US\":\"ignored\"}}]}]",
            "[{\"key\":\"" + ColorKey.ToUpperInvariant() + "\",\"values\":{\"id\":\"b\"}}]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":\"missing\"},{\"other\":1},\"text\",null,{\"id\":null}]}]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[]}]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":\"u\"}]}]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":7}]}]",
            "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":\"a\"}]},{\"key\":\"" + TextKey + "\",\"values\":\"Machine wash\"}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"\"},{\"key\":\"" + TextKey + "\",\"values\":null}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":12.50}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":true}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"2024-01-01T00:00:00Z\"}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"line\\nbreak \\u00e9 &amp;\"}]",
            "[{\"key\":\"" + ColorKey + "\",\"Key\":\"" + TextKey + "\",\"values\":\"last key wins\"}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"first\",\"Values\":\"last values win\"}]",
            "[{\"KEY\":\"" + TextKey + "\",\"values\":\"upper case name is not key\"}]",
            "[{\"key\":\"not-a-guid\",\"values\":\"x\"},{\"key\":null,\"values\":\"x\"},{\"values\":\"no key\"}]",
            "[{\"key\":\"33333333-3333-3333-3333-333333333333\",\"values\":\"unknown field\"}]",
            "[{\"key\":\"33333333-3333-3333-3333-333333333333\"}]",
            "[{\"key\":\"" + TextKey + "\"}]",
            "[{\"key\":123,\"values\":\"x\"}]",
            "[\"text\"]",
            "[1,2]",
            "{\"key\":\"" + TextKey + "\",\"values\":\"object root\"}",
            "[{'key':'" + TextKey + "','values':'single quotes'}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"trailing comma\",}]",
            "[{\"key\":\"" + TextKey + "\",\"values\":\"unterminated\"}",
            "not json",
        ];

        return values.Select(x => new object[] { x });
    }

    [Theory]
    [MemberData(nameof(MetafieldJson))]
    public void ParseFastPath_MatchesJTokenParser(string json)
    {
        var fields = new List<Metafield>
        {
            new(MetafieldNode("Color", ColorKey, "[{\"id\":\"a\",\"values\":{\"en-US\":\"Red\",\"is-IS\":\"Rauður\"}},{\"id\":\"b\",\"values\":{\"en-US\":\"Blue\"}},{\"id\":\"u\",\"values\":{\"undefined\":\"x\",\"en-US\":\"Kept\"}},{\"id\":\"7\",\"values\":{\"en-US\":\"Seven\"}}]")),
            new(MetafieldNode("Text", TextKey, "")),
        };
        // Put "undefined" back, as stored definitions can still carry it when read.
        fields[0].Values.Single(x => x.Id == "u").Values["undefined"] = "x";

        string? expected;
        try
        {
            expected = JsonConvert.SerializeObject(MetafieldService.ParseMetafieldsWithJToken(json, fields));
        }
        catch (Exception)
        {
            expected = null;
        }

        var handled = MetafieldService.TryParseMetafields(json, fields, out var actual);

        if (expected == null)
            Assert.False(handled);
        else if (handled)
            Assert.Equal(expected, JsonConvert.SerializeObject(actual));
    }

    [Fact]
    public void ParseFastPath_HandlesPickerOutputAndCopiesValueDictionaries()
    {
        var fields = new List<Metafield> { new(MetafieldNode("Color", ColorKey, "[{\"id\":\"a\",\"values\":{\"en-US\":\"Red\"}}]")) };
        var json = "[{\"key\":\"" + ColorKey + "\",\"values\":[{\"id\":\"a\"}]}]";

        Assert.True(MetafieldService.TryParseMetafields(json, fields, out var parsed));

        var value = Assert.Single(Assert.Single(parsed).Values);
        Assert.Equal("Red", value["en-US"]);
        Assert.NotSame(fields[0].Values[0].Values, value);
    }

    private static MetafieldService CreateService(INodeService nodes)
        => new(nodes, new MemoryCache(new MemoryCacheOptions()));

    private static UmbracoContent MetafieldNode(string name, string? key = null, string? values = null)
        => new(new Dictionary<string, string>
        {
            ["id"] = name.Length.ToString(),
            ["__Key"] = key ?? Guid.NewGuid().ToString(),
            ["nodeName"] = name,
        }, new Dictionary<string, string>
        {
            ["title"] = "{\"values\":{\"en-US\":\"" + name + "\"},\"type\":\"Language\"}",
            ["alias"] = name.ToLowerInvariant(),
            ["filterable"] = "1",
            ["values"] = values ?? "[{\"id\":\"a\",\"values\":{\"en-US\":\"10\"}},{\"id\":\"b\",\"values\":{\"en-US\":\"9\"}}]",
        });

    private static MetafieldSlim Field(int id, string alias, bool allMustMatch = false, bool filterable = true)
        => new() { Id = id, Key = Guid.NewGuid(), Alias = alias, Name = alias, Filterable = filterable, AllConditionsMustMatch = allMustMatch };

    private static IProduct Product(int id, params (MetafieldSlim Field, string[] Values)[] metafields)
    {
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Id).Returns(id);
        product.SetupGet(x => x.Metafields).Returns(metafields
            .Select(x => new MetavalueSlim
            {
                Field = x.Field,
                Values = x.Values.Select(v => new Dictionary<string, string> { ["en-US"] = v, ["is-IS"] = v + " (IS)" }).ToList(),
            })
            .ToList());
        return product.Object;
    }

    private static List<IProduct> CreateProducts(int seed, int count)
    {
        var random = new Random(seed);
        // Two instances per field with the same key, as each product carries its own copy.
        var fields = Enumerable.Range(0, 6)
            .Select(i => (Key: Guid.NewGuid(), Alias: "field" + i, Filterable: i != 4))
            .ToList();
        string[] pool = ["10", "9", "2,5", "2.5", "Red", "red", "Blue", "XL", "", "100", "A", "b"];

        var products = new List<IProduct>();
        for (var p = 0; p < count; p++)
        {
            var metafields = new List<MetavalueSlim>();
            foreach (var field in fields.OrderBy(_ => random.Next()).Take(random.Next(0, fields.Count + 1)))
            {
                var values = new List<Dictionary<string, string>>();
                for (var v = random.Next(0, 4); v > 0; v--)
                {
                    var text = pool[random.Next(pool.Length)];
                    var value = new Dictionary<string, string> { ["en-US"] = text, ["is-IS"] = text + " (IS)" };
                    if (random.Next(0, 12) == 0) value = new Dictionary<string, string> { ["undefined"] = text };
                    values.Add(value);
                }

                metafields.Add(new MetavalueSlim
                {
                    Field = new MetafieldSlim { Key = field.Key, Alias = field.Alias, Name = field.Alias + p, Filterable = field.Filterable },
                    Values = values,
                });
            }

            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Metafields).Returns(metafields);
            products.Add(product.Object);
        }

        return products;
    }

    // The implementation this replaced, kept as the behavioural reference.
    private static IEnumerable<MetafieldGrouped> ReferenceFilters(IEnumerable<IProduct> products, bool filterable)
    {
        var metafields = products
            .SelectMany(x => x.Metafields)
            .Where(x => x.Field.Filterable == filterable)
            .ToList();

        foreach (var group in metafields.GroupBy(x => x.Field, new MetafieldComparer()))
        {
            yield return new MetafieldGrouped
            {
                Field = group.Key,
                Values = group
                    .SelectMany(x => x.Values)
                    .Where(x => !x.ContainsKey("undefined"))
                    .DistinctBy(x => x.Values.FirstOrDefault())
                    .OrderBy(x => x.Values.FirstOrDefault(), new SemiNumericComparer())
                    .ToList(),
            };
        }
    }
}
