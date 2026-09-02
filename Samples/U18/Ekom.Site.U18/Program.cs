using Ekom.Algolia;
using Ekom.Klaviyo;
using Ekom.Services;
using Ekom.Site.U17;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

builder.Services.AddTransient<IProductFilterService, CustomProductFilterService>();
builder.Services.AddAlgolia();
builder.Services.AddKlaviyo();

var app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
