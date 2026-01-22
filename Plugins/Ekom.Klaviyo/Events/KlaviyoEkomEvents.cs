using Ekom.Events;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Klaviyo.Events;
internal class KlaviyoEkomEvents : IComponent
{
    public void Initialize()
    {
        CatalogEvents.BeforeReturnCategory += OnBeforeReturnCategory;
        CatalogEvents.BeforeReturnCategories += OnBeforeReturnCategories;
        CatalogEvents.BeforeReturnProduct += OnBeforeReturnProduct;
        OrderEvents.AddingOrderline += OnAddingOrderline;
    }

    private void OnAddingOrderline(object? sender, AddingOrderlineEventArgs e)
    {

    }

    private void OnBeforeReturnProduct(object? sender, ProductEventArgs e)
    {
    }

    private void OnBeforeReturnCategories(object? sender, CategoriesEventArgs e)
    {
    }

    private void OnBeforeReturnCategory(object? sender, CategoryEventArgs e)
    {
    }

    public void Terminate()
    {
        CatalogEvents.BeforeReturnCategory -= OnBeforeReturnCategory;
        CatalogEvents.BeforeReturnCategories -= OnBeforeReturnCategories;
        CatalogEvents.BeforeReturnProduct -= OnBeforeReturnProduct;
        OrderEvents.AddingOrderline -= OnAddingOrderline;
    }
}

internal class KlaviyoEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<KlaviyoEkomEvents>();
    }
}
