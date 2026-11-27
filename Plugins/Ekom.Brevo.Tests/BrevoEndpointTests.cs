using Ekom.Brevo.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Ekom.Brevo.Tests;

public sealed class BrevoEndpointTests
{
    [Fact]
    public async Task MappedEndpointResolvesServicesAndEnforcesBearerAuthentication()
    {
        using var database = new BrevoTestDatabase();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(database.Factory);
        builder.Services.AddBrevo(o =>
        {
            o.Enabled = true;
            o.Transactional.Webhooks.Enabled = true;
            o.Transactional.Webhooks.Token = new string('x', 40);
        });
        await using var app = builder.Build();
        app.MapBrevoWebhooks();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = Address(app) };
            var endpoint = new Uri("/ekom/brevo/webhooks/shop", UriKind.Relative);
            using var body = new StringContent("[]");
            using var unauthorized = await client.PostAsync(endpoint, body);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new string('x', 40));
            using var authorizedBody = new StringContent("[]");
            using var accepted = await client.PostAsync(endpoint, authorizedBody);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        finally { await app.StopAsync(); }
    }
    private static Uri Address(WebApplication app)
        => new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
}
