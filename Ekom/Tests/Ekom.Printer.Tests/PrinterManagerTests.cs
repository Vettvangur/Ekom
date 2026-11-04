using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Services;
using Ekom.Printer.Storage;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Ekom.Printer.Api;

namespace Ekom.Printer.Tests;

public sealed class PrinterManagerTests
{
    [Fact]
    public async Task ManagerApiRequiresPermissionsAndAntiforgeryAndDoesNotExposeSecrets()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await using var db = fixture.Factory.GetDatabase();
        await db.CreateTableAsync<OrderData>();
        var orderId = Guid.NewGuid();
        await db.InsertAsync(new OrderData { UniqueId = orderId, StoreAlias = "allowed", OrderNumber = "123",
            OrderStatusCol = "ReadyForDispatch", OrderInfo = "{}" });
        var jobId = new string('c', 64);
        await db.InsertAsync(new PrintJob { Id = jobId, OrderId = orderId, StationAlias = "warehouse",
            State = PrintJobState.Ready, Html = "<p>Customer slip</p>", CreatedUtc = DateTime.UtcNow });
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessManager()).Returns(true);
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(new[] { "allowed" });
        access.Setup(x => x.CanAccessStore("allowed")).Returns(true);
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.IsCurrentUserAdmin()).Returns(true);
        security.Setup(x => x.GetUmbracoUserGroups()).Returns(Array.Empty<string>());
        var options = new PrinterOptions { Enabled = true, Stations = [new PrinterStationOptions {
            Alias = "warehouse", StoreAliases = ["allowed"], TokenSha256 = new string('f', 64),
        }] };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization(x => x.AddPolicy("UmbracoUser", p => p.RequireAuthenticatedUser()));
        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton(fixture.Factory);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IOptions<PrinterOptions>>(Options.Create(options));
        builder.Services.AddSingleton(access.Object);
        builder.Services.AddSingleton(security.Object);
        builder.Services.AddScoped<PrinterManagerService>();
        builder.Services.AddSingleton<PrinterDeliveryStore>();
        await using var app = builder.Build();
        // Test-only identity. Production uses the host's UmbracoUser authentication policy.
        app.Use((context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin") }, "test"));
            return next(context);
        });
        app.UseAuthorization();
        app.MapEkomPrinter();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        const string root = "/ekom/printer/manager";
        var response = await client.GetAsync(root + "/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var capability = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stations = await client.GetStringAsync(root + "/stations");
        Assert.DoesNotContain(options.Stations[0].TokenSha256, stations, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenSha256", stations, StringComparison.OrdinalIgnoreCase);
        var action = new PrinterManagerRequest("cancel", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(root + "/jobs/" + jobId + "/actions", action)).StatusCode);
        client.DefaultRequestHeaders.Add(capability.GetProperty("csrfHeader").GetString()!, capability.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(root + "/jobs/" + jobId + "/actions", action)).StatusCode);
        security.Setup(x => x.IsCurrentUserAdmin()).Returns(false);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/jobs/" + jobId + "/actions", action)).StatusCode);
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(Array.Empty<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(root + "/jobs/" + jobId + "/preview")).StatusCode);
        access.Setup(x => x.CanAccessManager()).Returns(false);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(root + "/capabilities")).StatusCode);
        options.Enabled = false;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(root + "/capabilities")).StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task ManagerScopesJobsAndAuditsIdempotentReprints()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await using var db = fixture.Factory.GetDatabase();
        await db.CreateTableAsync<OrderData>();
        var order = Guid.NewGuid();
        var otherOrder = Guid.NewGuid();
        foreach (var pair in new[] { (order, "allowed"), (otherOrder, "other") })
            await db.InsertAsync(new OrderData { UniqueId = pair.Item1, StoreAlias = pair.Item2,
                OrderNumber = "123", OrderStatusCol = "ReadyForDispatch", OrderInfo = "{}" });
        var job = new PrintJob { Id = new string('a', 64), OrderId = order, StationAlias = "warehouse",
            State = PrintJobState.Submitted, Html = "<p>Exact original</p>", Copies = 2, CreatedUtc = DateTime.UtcNow };
        await db.InsertAsync(job);
        await db.InsertAsync(new PrintJob { Id = new string('b', 64), OrderId = otherOrder,
            State = PrintJobState.Ready, CreatedUtc = DateTime.UtcNow });
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(new[] { "allowed" });
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.IsCurrentUserAdmin()).Returns(true);
        security.Setup(x => x.GetUmbracoUserGroups()).Returns(Array.Empty<string>());
        var manager = new PrinterManagerService(fixture.Factory, access.Object, security.Object,
            Options.Create(new PrinterOptions()), TimeProvider.System);
        Assert.Single((await manager.ListAsync(null, null, null, 1)).Jobs);
        Assert.Null(await manager.FindAsync(new string('b', 64)));
        var request = new PrinterManagerRequest("reprint", Guid.NewGuid());
        var newId = await manager.ActAsync(job.Id, request, "Admin");
        Assert.NotNull(newId);
        Assert.Equal(newId, await manager.ActAsync(job.Id, request, "Admin"));
        Assert.Null(await manager.ActAsync(job.Id, request, "Someone else"));
        Assert.Equal(3, await db.GetTable<PrintJob>().CountAsync());
        var copy = await db.GetTable<PrintJob>().SingleAsync(x => x.Id == newId);
        Assert.Equal(job.Html, copy.Html);
        Assert.Equal(2, copy.Copies);
        Assert.Equal(PrintJobState.Ready, copy.State);
        Assert.Equal(1, await db.GetTable<PrinterManagerAction>().CountAsync());
        security.Setup(x => x.IsCurrentUserAdmin()).Returns(false);
        Assert.Null(await manager.ActAsync(copy.Id, new PrinterManagerRequest("cancel", Guid.NewGuid()), "Reader"));
        security.Setup(x => x.IsCurrentUserAdmin()).Returns(true);
        await db.GetTable<PrintJob>().Where(x => x.Id == copy.Id).Set(x => x.State, PrintJobState.Claimed).UpdateAsync();
        Assert.Null(await manager.ActAsync(copy.Id, new PrinterManagerRequest("cancel", Guid.NewGuid()), "Admin"));
    }
}
