using Ekom.Models;
using Ekom.Utilities;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class BackofficeStoreAvailabilityTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("y", true)]
    [InlineData("TRUE", true)]
    [InlineData("Enable", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("Y", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CurrentCategoryPreservesDisableTruthConversion(string? disable, bool disabled)
    {
        var store = CreateStore("is");
        var node = CreateNode("ekmCategory", disable);

        var result = BackofficeStoreAvailability.FilterEnabledStores(node, Array.Empty<UmbracoContent>(), new[] { store });

        Assert.Equal(disabled ? Array.Empty<IStore>() : new[] { store }, result);
    }

    [Theory]
    [InlineData("ekmCategory")]
    [InlineData("ekmProduct")]
    [InlineData("ekmProductVariant")]
    public void DisabledAncestorCategoryExcludesStore(string contentTypeAlias)
    {
        var node = CreateNode(contentTypeAlias, "false");
        var ancestors = new[] { CreateNode("ekmProduct", "false"), CreateNode("ekmCategory", "true") };

        var result = BackofficeStoreAvailability.FilterEnabledStores(node, ancestors, new[] { CreateStore("is") });

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("ekmProduct")]
    [InlineData("ekmProductVariant")]
    [InlineData("ekmProductVariantGroup")]
    [InlineData("customContent")]
    public void DisabledNonCategoryCurrentNodeIsIgnored(string contentTypeAlias)
    {
        var store = CreateStore("is");
        var node = CreateNode(contentTypeAlias, "true");

        var result = BackofficeStoreAvailability.FilterEnabledStores(node, Array.Empty<UmbracoContent>(), new[] { store });

        Assert.Same(store, Assert.Single(result));
    }

    [Theory]
    [InlineData("ekmCategory")]
    [InlineData("ekmProduct")]
    [InlineData("ekmProductVariant")]
    public void DisabledProductAndNonCategoryAncestorsAreIgnored(string contentTypeAlias)
    {
        var store = CreateStore("is");
        var node = CreateNode(contentTypeAlias, "false");
        var ancestors = new[]
        {
            CreateNode("ekmProduct", "true"),
            CreateNode("customContent", "true"),
            CreateNode("ekmCategory", "false"),
        };

        var result = BackofficeStoreAvailability.FilterEnabledStores(node, ancestors, new[] { store });

        Assert.Same(store, Assert.Single(result));
    }

    [Fact]
    public void MixedStoresPreserveAliasMatchingOrderAndDuplicates()
    {
        var node = CreateNode("ekmCategory", "{\"IS\":true}");
        var ancestors = new[]
        {
            CreateNode("ekmProduct", "true"),
            CreateNode("ekmCategory", "{\"UK\":\"1\",\"us\":false}"),
        };
        var stores = new[] { CreateStore("us"), CreateStore("is"), CreateStore("dk"), CreateStore("uk"), CreateStore("US") };

        var result = BackofficeStoreAvailability.FilterEnabledStores(node, ancestors, stores);

        Assert.Equal(new[] { stores[0], stores[2], stores[4] }, result);
    }

    private static UmbracoContent CreateNode(string contentTypeAlias, string? disable)
    {
        var properties = new Dictionary<string, string>();
        if (disable != null)
        {
            properties["disable"] = disable;
        }

        return new UmbracoContent(new Dictionary<string, string>(), properties)
        {
            ContentTypeAlias = contentTypeAlias,
        };
    }

    private static IStore CreateStore(string alias)
    {
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns(alias);
        return store.Object;
    }
}
