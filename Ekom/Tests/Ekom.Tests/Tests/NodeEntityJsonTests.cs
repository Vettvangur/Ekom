using Ekom.Models;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class NodeEntityJsonTests
{
    public static IEnumerable<object[]> Entities() =>
    [
        [typeof(IProduct), Mock.Of<IProduct>()],
        [typeof(IVariant), Mock.Of<IVariant>()],
        [typeof(IVariantGroup), Mock.Of<IVariantGroup>()],
        [typeof(ICategory), Mock.Of<ICategory>()],
    ];

    [Theory]
    [MemberData(nameof(Entities))]
    public void InterfaceTypedSerialization_HidesRawPropertiesAndPath(Type type, object entity)
    {
        // System.Text.Json serializes nested entities by their declared interface, where
        // the JsonIgnore attributes on the classes do not apply.
        var json = JsonSerializer.Serialize(entity, type, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(x => x.Name).ToList();

        Assert.DoesNotContain("properties", names);
        Assert.DoesNotContain("path", names);
        Assert.Contains("id", names);
        Assert.Contains("title", names);
        Assert.Contains("pathArray", names);
    }
}
