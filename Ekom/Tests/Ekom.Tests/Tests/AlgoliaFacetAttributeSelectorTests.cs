using Ekom.Algolia;
using Ekom.Algolia.Events;
using Ekom.Algolia.Indexing;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaFacetAttributeSelectorTests
{
    [Fact]
    public void Adds_Filterable_Metafields_Without_Duplicating_Explicit_Facets()
    {
        var attributes = AlgoliaFacetAttributeSelector.Merge(
            ["brand", "metafield:Material|array", "material"],
            ["MATERIAL", "color", "COLOR", " "]);

        Assert.Equal(["brand", "material", "metafield:color"], attributes);
    }

    [Fact]
    public void Generates_Index_Settings_For_Automatic_And_Explicit_Facets()
    {
        var options = new AlgoliaIndexingOptions { FacetAttributes = ["brand"] };
        var selected = AlgoliaFacetAttributeSelector.Merge(options.FacetAttributes, ["material"]);

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options, selected);

        Assert.Contains("attributes.brand", attributes);
        Assert.Contains("attributes.material", attributes);
    }

    [Fact]
    public void Generates_AfterDistinct_Facets_For_Variant_Indexes()
    {
        var options = new AlgoliaIndexingOptions { Variants = true };
        var selected = AlgoliaFacetAttributeSelector.Merge([], ["material"]);

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options, selected);

        Assert.Contains("afterDistinct(attributes.material)", attributes);
    }

    [Fact]
    public void Explicit_Metafield_Modifier_Takes_Precedence()
    {
        var attributes = AlgoliaFacetAttributeSelector.Merge(["metafield:material|array"], ["MATERIAL"]);

        Assert.Equal(["metafield:material|array"], attributes);
    }

    [Fact]
    public void Removing_Filterable_Flag_Removes_Only_Automatic_Facet()
    {
        var attributes = AlgoliaFacetAttributeSelector.Merge(["metafield:brand"], []);

        Assert.Equal(["metafield:brand"], attributes);
        Assert.Empty(AlgoliaFacetAttributeSelector.Merge([], []));
    }

    [Fact]
    public void Metafield_Definition_Alias_Is_Recognized()
    {
        Assert.True(AlgoliaUmbracoNotifications.IsMetafieldDefinition("EKMMETAFIELD"));
        Assert.False(AlgoliaUmbracoNotifications.IsMetafieldDefinition("ekmProduct"));
    }
}
