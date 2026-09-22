using Ekom.Models.Import;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportProductTests
{
    [Fact]
    public void PreservePrimaryCategory_DefaultsToFalse()
    {
        var product = new ImportProduct
        {
            Identifier = "product-identifier",
            NodeName = "Product",
            Title = [],
        };

        Assert.False(product.PreservePrimaryCategory);
    }
}
