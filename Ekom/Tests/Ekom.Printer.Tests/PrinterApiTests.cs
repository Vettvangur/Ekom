using Ekom.Printer.Api;
using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Protocol;
using Ekom.Printer.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Ekom.Printer.Tests;

public sealed class PrinterApiTests
{
    [Fact]
    [SuppressMessage("Maintainability", "CA1506", Justification = "End-to-end HTTP test deliberately exercises host, auth, protocol and persistence together.")]
    [SuppressMessage("Usage", "CA2234", Justification = "Test routes are relative strings composed with the test client's ephemeral base address.")]
    public async Task ApiRequiresStationCredentialVersionAndValidAcknowledgement()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await DeliveryTests.ReadyAsync(new PrinterJobStore(fixture.Factory));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new PrinterOptions
        {
            Enabled = true,
            Stations = [new PrinterStationOptions
            {
                Alias = "warehouse",
                TokenSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            }],
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(fixture.Factory);
        builder.Services.AddSingleton<IOptions<PrinterOptions>>(Options.Create(options));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<PrinterDeliveryStore>();
        builder.Services.AddAntiforgery();
        builder.Services.AddScoped<Ekom.Printer.Services.PrinterManagerService>();
        builder.Services.AddSingleton(Moq.Mock.Of<Ekom.Services.IManagerAccessService>());
        builder.Services.AddSingleton(Moq.Mock.Of<Ekom.Services.ISecurityService>());
        await using var app = builder.Build();
        // Simulate TLS termination by a trusted proxy. Production must configure forwarded headers explicitly.
        app.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey("Test-Plain-Http")) context.Request.Scheme = "https";
            await next();
        });
        app.MapEkomPrinter();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        const string root = "/ekom/printer/v1/stations/warehouse";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(root + "/claim", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(root + "/claim", null)).StatusCode);
        client.DefaultRequestHeaders.Add(PrinterProtocol.VersionHeader, "2");
        Assert.Equal(HttpStatusCode.UpgradeRequired, (await client.PostAsync(root + "/claim", null)).StatusCode);
        client.DefaultRequestHeaders.Remove(PrinterProtocol.VersionHeader);
        client.DefaultRequestHeaders.Add(PrinterProtocol.VersionHeader, "1");
        client.DefaultRequestHeaders.Add("Test-Plain-Http", "true");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(root + "/claim", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Test-Plain-Http");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/ekom/printer/v1/stations/other/claim", null)).StatusCode);
        var response = await client.PostAsync(root + "/claim", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var job = Assert.IsType<ClaimedPrintJob>(await response.Content.ReadFromJsonAsync<ClaimedPrintJob>());
        Assert.False(job.Policy.JavaScriptEnabled);
        Assert.False(job.Policy.ExternalNetworkRequestsEnabled);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(root + "/claim", null)).StatusCode);
        var path = root + "/jobs/" + job.JobId + "/acknowledge";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new PrintAcknowledgement(job.ClaimToken, true, null))).StatusCode);
        var ack = new PrintAcknowledgement(job.ClaimToken, true, "123");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path, ack)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path, ack)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, ack with { SpoolJobId = "changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/heartbeat", new AgentHeartbeat(1, "test", "Office"))).StatusCode);
        var health = await client.GetFromJsonAsync<StationHealth>(root + "/health");
        Assert.True(health?.Online);
        options.Enabled = false;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(root + "/claim", null)).StatusCode);
        await app.StopAsync();
    }
}
