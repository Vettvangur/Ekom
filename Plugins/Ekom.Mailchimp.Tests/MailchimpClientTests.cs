using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Http;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpClientTests
{
    [Fact]
    public async Task SubscribeAsync_UpsertsMemberAndAppliesTags()
    {
        using var handler = new RecordingHandler();
        MailchimpAudienceClient client = CreateAudienceClient(handler);

        await client.SubscribeAsync(new MailchimpSubscribeRequest
        {
            StoreAlias = "default",
            Email = "Person@Example.com",
            Status = MailchimpSubscriptionStatus.Pending,
            FirstName = "Person",
            Tags = ["customer", "Customer"],
        }, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        RecordedRequest member = handler.Requests[0];
        Assert.Equal(HttpMethod.Put, member.Method);
        Assert.Contains("/lists/audience/members/", member.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("pending", JsonNode.Parse(member.Body!)!["status"]!.GetValue<string>());
        Assert.Equal("pending", JsonNode.Parse(member.Body!)!["status_if_new"]!.GetValue<string>());
        Assert.Equal("Basic", member.Authorization?.Scheme);

        RecordedRequest tags = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, tags.Method);
        Assert.EndsWith("/tags", tags.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Single(JsonNode.Parse(tags.Body!)!["tags"]!.AsArray());
    }

    [Fact]
    public async Task TrackPurchaseAsync_UpsertsDependenciesBeforeOrder()
    {
        using var handler = new RecordingHandler();
        MailchimpEcommerceClient client = CreateEcommerceClient(handler);
        MailchimpPurchase purchase = CreatePurchase();

        await client.TrackPurchaseAsync(purchase, CancellationToken.None);

        Assert.Collection(
            handler.Requests,
            request => Assert.Equal(HttpMethod.Get, request.Method),
            request => Assert.Contains("/customers/", request.Uri.AbsolutePath, StringComparison.Ordinal),
            request => Assert.Contains("/products/", request.Uri.AbsolutePath, StringComparison.Ordinal),
            request => Assert.Contains("/orders/", request.Uri.AbsolutePath, StringComparison.Ordinal));
        Assert.All(handler.Requests.Skip(1), request => Assert.Equal(HttpMethod.Put, request.Method));

        JsonNode customer = JsonNode.Parse(handler.Requests[1].Body!)!;
        Assert.Equal("person@example.com", customer["email_address"]!.GetValue<string>());
        Assert.False(customer["opt_in_status"]!.GetValue<bool>());

        JsonNode product = JsonNode.Parse(handler.Requests[2].Body!)!;
        Assert.Equal("variant-1", product["variants"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(10, product["variants"]![0]!["price"]!.GetValue<decimal>());

        JsonNode order = JsonNode.Parse(handler.Requests[3].Body!)!;
        Assert.Equal("USD", order["currency_code"]!.GetValue<string>());
        Assert.Equal(1, order["lines"]![0]!["quantity"]!.GetValue<int>());
    }

    private static MailchimpAudienceClient CreateAudienceClient(HttpMessageHandler handler)
    {
        var httpClient = new MailchimpHttpClient(new TestHttpClientFactory(handler), NullLogger<MailchimpHttpClient>.Instance);
        return new MailchimpAudienceClient(CreateResolver(), httpClient);
    }

    private static MailchimpEcommerceClient CreateEcommerceClient(HttpMessageHandler handler)
    {
        var httpClient = new MailchimpHttpClient(new TestHttpClientFactory(handler), NullLogger<MailchimpHttpClient>.Instance);
        return new MailchimpEcommerceClient(CreateResolver(), httpClient);
    }

    private static MailchimpConfigurationResolver CreateResolver() => new(
        Options.Create(new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "secret-us1",
            AudienceId = "audience",
            EcommerceStoreId = "store",
        }),
        NullLogger<MailchimpConfigurationResolver>.Instance);

    private static MailchimpPurchase CreatePurchase() => new()
    {
        StoreAlias = "default",
        StoreName = "Default",
        OrderId = "order-1",
        CurrencyCode = "USD",
        OrderTotal = 10,
        ProcessedAt = DateTimeOffset.UtcNow,
        Customer = new MailchimpPurchaseCustomer
        {
            Id = "customer-1",
            Email = "person@example.com",
        },
        Lines =
        [
            new MailchimpPurchaseLine
            {
                Id = "line-1",
                ProductId = "product-1",
                ProductVariantId = "variant-1",
                ProductTitle = "Product",
                VariantTitle = "Variant",
                Quantity = 1,
                Price = 10,
            },
        ],
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                body,
                request.Headers.Authorization));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Body,
        AuthenticationHeaderValue? Authorization);

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
