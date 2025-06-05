using Ekom.Events;
using Ekom.Exceptions;
using Umbraco.Cms.Core.Composing;

namespace Ekom.Site;

public class EkomEvents : IComponent
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
        
        //throw new EkomProblemDetailsException("Title", "Description", System.Net.HttpStatusCode.BadGateway);
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

public class EkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<EkomEvents>();
    }
}
