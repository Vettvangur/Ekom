using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Umb.Services;
using Ekom.Umb.VariantApp.Models;
using Ekom.Umb.VariantApp.Services;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection(NativeDiscountPriceCollection.Name)]
public sealed class VariantPriceWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DraftAccessDoesNotRequireLivePricePermission(bool databasePrices)
    {
        using var db = new DatabasePriceTests.Fixture(databasePrices);
        var fixture = new Workflow();
        Assert.Same(fixture.Variant.Object, fixture.Invoke("GetContent", "3", "ekmProductVariant"));
        fixture.Invoke("SaveContent", fixture.Variant.Object, false);
        fixture.Content.Verify(x => x.Save(fixture.Variant.Object, It.IsAny<int?>(), null), Times.Once);
        var error = Assert.Throws<HttpResponseException>(() => fixture.Invoke("SaveContent", fixture.Variant.Object, true));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public void MetadataOnlyPriceBaselineDoesNotRequirePublishButPriceChangesDo()
    {
        using var db = new DatabasePriceTests.Fixture();
        var fixture = new Workflow();
        var model = fixture.Model(50);
        fixture.Invoke("ApplyDatabasePrices", fixture.Variant.Object, model, fixture.Stores);
        model.PriceValues["Web"][0].Price = 60;
        var error = Assert.Throws<HttpResponseException>(() => fixture.Invoke("ApplyDatabasePrices", fixture.Variant.Object, model, fixture.Stores));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Empty(db.Prices.Get(fixture.Variant.Object.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidRequiredFieldRejectsEntireSubmittedFormBeforePrices(bool bulk)
    {
        using var db = new DatabasePriceTests.Fixture();
        var fixture = new Workflow(requiredField: true);
        fixture.Security.Setup(x => x.CanEditPrices(It.IsAny<UmbracoContent>())).Returns(true);
        db.Prices.SetPrice(fixture.Variant.Object.Key, "Web", "en-US", 50);
        var model = fixture.Model(60);
        model.PriceOverrides = db.Prices.Get(fixture.Variant.Object.Key);
        var earlierValid = fixture.Model(70);
        earlierValid.PriceOverrides = model.PriceOverrides;
        earlierValid.CustomFields = [new VariantManagerCustomField { Alias = "required", Value = "valid" }];
        var error = await Assert.ThrowsAsync<HttpResponseException>(() => bulk
            ? fixture.Service.SaveProductVariantsAsync(new VariantManagerSaveRequest
            {
                ProductId = "1", Groups = [new VariantManagerGroup { Id = 2, Variants = [earlierValid, model] }],
            })
            : fixture.Service.SaveVariantAsync(new VariantManagerVariantSaveRequest { GroupId = "2", Variant = model }));
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal(50, Assert.Single(db.Prices.Get(fixture.Variant.Object.Key)).Price);
        fixture.Content.Verify(x => x.Save(It.IsAny<IContent>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection>()), Times.Never);
    }

    [Fact]
    public void VariantResponseFiltersCurrentOriginalAndOverridePricesByStoreAccess()
    {
        using var db = new DatabasePriceTests.Fixture();
        var fixture = new Workflow();
        db.Prices.SetPrice(fixture.Variant.Object.Key, "Web", "en-US", 50);
        db.Prices.SetPrice(fixture.Variant.Object.Key, "Wholesale", "en-US", 10);
        var stores = fixture.Stores.Concat([new VariantManagerStore { Alias = "Wholesale", Currencies = fixture.Stores[0].Currencies }]).ToList();
        var result = (VariantManagerVariant)fixture.Invoke("MapVariant", fixture.Variant.Object, stores,
            Array.Empty<UmbracoLanguage>(), Array.Empty<VariantManagerCustomFieldDefinition>())!;
        Assert.Equal("Web", Assert.Single(result.PriceValues).Key);
        Assert.Equal("Web", Assert.Single(result.OriginalPriceValues!).Key);
        Assert.Equal("WEB", Assert.Single(result.PriceOverrides!).StoreAlias);
    }

    private sealed class Workflow
    {
        public Mock<IContentService> Content { get; } = new();
        public Mock<ISecurityService> Security { get; } = new();
        public Mock<IContent> Variant { get; } = Node(3, 2, "ekmProductVariant");
        public IVariantAppService Service { get; }
        public IReadOnlyList<VariantManagerStore> Stores { get; } = [new() { Alias = "Web", Currencies = [new CurrencyModel { CurrencyValue = "en-US" }] }];

        public Workflow(bool requiredField = false)
        {
            Content.Setup(x => x.GetById(1)).Returns(Node(1, -1, "ekmProduct").Object);
            Content.Setup(x => x.GetById(2)).Returns(Node(2, 1, "ekmProductVariantGroup").Object);
            Content.Setup(x => x.GetById(3)).Returns(Variant.Object);
            Security.Setup(x => x.CanEditContent(It.IsAny<UmbracoContent>())).Returns(true);
            var access = new Mock<IManagerAccessService>();
            access.Setup(x => x.CanAccessStore(It.IsAny<string>())).Returns((string alias) => alias.Equals("Web", StringComparison.OrdinalIgnoreCase));
            var types = new Mock<IContentTypeService>();
            var property = Mock.Of<IPropertyType>(x => x.Alias == "required" && x.Name == "Required" && x.Mandatory && x.PropertyEditorAlias == "Umbraco.TextBox");
            var type = new Mock<IContentType>();
            type.SetupGet(x => x.CompositionPropertyTypes).Returns([property]);
            types.Setup(x => x.Get("ekmProductVariant")).Returns(type.Object);
            var options = new EkomOptions();
            if (requiredField) options.VariantApp.Variants.Add("required");
            var serviceType = typeof(ImportService).Assembly.GetType("Ekom.Umb.VariantApp.Services.VariantAppService")!;
            Service = (IVariantAppService)Activator.CreateInstance(serviceType, Content.Object, types.Object,
                Mock.Of<INodeService>(), Mock.Of<ILocalizationService>(), Options.Create(options), Security.Object, access.Object)!;
        }

        public VariantManagerVariant Model(decimal price) => new()
        {
            Id = 3, Title = "Variant", PriceValues = new() { ["Web"] = [new CurrencyPrice(price, "en-US")] },
            OriginalPriceValues = new() { ["Web"] = [new CurrencyPrice(50, "en-US")] }, PriceOverrides = [],
        };

        public object? Invoke(string name, params object?[] args)
        {
            var method = Service.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(x => x.Name == name && x.GetParameters().Length == args.Length);
            try { return method.Invoke(Service, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }

        private static Mock<IContent> Node(int id, int parent, string alias)
        {
            var node = new Mock<IContent>();
            node.SetupGet(x => x.Id).Returns(id);
            node.SetupGet(x => x.ParentId).Returns(parent);
            node.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            node.SetupGet(x => x.Path).Returns("-1,1,2,3");
            node.SetupGet(x => x.Name).Returns("Variant");
            node.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == alias));
            node.SetupGet(x => x.Properties).Returns(new PropertyCollection());
            node.SetupGet(x => x.AvailableCultures).Returns([]);
            return node;
        }
    }
}
