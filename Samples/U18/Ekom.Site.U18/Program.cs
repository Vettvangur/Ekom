using Ekom.Algolia;
using Ekom.Interfaces;
using Ekom.Klaviyo;
using Ekom.Mailchimp;
using Ekom.Models;
using Ekom.Services;
using Ekom.Printer;
using Ekom.Printer.Api;
using Ekom.Site.U17;
using Ekom.Site.U17.EkomIntegration;
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
builder.Services.AddMailchimp();
builder.Services.AddEkomPrinter(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IPerStoreFactory<IProduct>, MemberProductFactory>();
    builder.Services.AddSingleton<IPerStoreFactory<IVariant>, MemberVariantFactory>();
}

var app = builder.Build();

await app.BootUmbracoAsync();
app.MapEkomPrinter();

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
