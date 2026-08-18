using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderLineVariantValidatorTests
{
    [Fact]
    public void Requires_Variant_When_Product_Has_Variants()
    {
        var product = CreateProduct([Mock.Of<IVariant>()]);

        var exception = Assert.Throws<VariantRequiredException>(() =>
            OrderLineVariantValidator.Validate(product.Object, null));

        Assert.Contains(product.Object.Key.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Allows_Missing_Variant_When_Product_Has_No_Variants()
    {
        var product = CreateProduct([]);

        OrderLineVariantValidator.Validate(product.Object, null);
    }

    [Fact]
    public void Allows_Missing_Variant_When_Product_Has_Only_Empty_Variant_Groups()
    {
        var product = CreateProduct([]);
        product.SetupGet(x => x.VariantGroups).Returns([Mock.Of<IVariantGroup>()]);

        OrderLineVariantValidator.Validate(product.Object, null);
    }

    [Fact]
    public void Allows_Selected_Variant_When_Product_Has_Variants()
    {
        var variant = Mock.Of<IVariant>();
        var product = CreateProduct([variant]);

        OrderLineVariantValidator.Validate(product.Object, variant);
    }

    [Fact]
    public void Variant_Required_Exception_Maps_To_Bad_Request()
    {
        const string message = "A variant is required for product 'product-key'.";

        var result = ExceptionHandler.Handle(new VariantRequiredException(message));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal(message, document.RootElement.GetProperty("message").GetString());
    }

    private static Mock<IProduct> CreateProduct(IReadOnlyCollection<IVariant> variants)
    {
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        product.SetupGet(x => x.AllVariants).Returns(variants);
        product.SetupGet(x => x.VariantGroups).Returns([]);
        return product;
    }
}
