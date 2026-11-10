using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Text.Json;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Ekom.Site.U17.Utility;

/// <summary>
/// Development-only load-test data: metafield definitions, metafield values on every product and
/// extra products. Runs once at startup when EKOM_SEED_PERF_DATA=1, then stops the site.
/// EKOM_SEED_PRODUCTS sets the total product count to reach (default 3000). Safe to run again.
/// Diagnostic mode, for a copy of the database only: EKOM_SEED_REPUBLISH_AT_STARTUP=N publishes
/// N products while Ekom's startup cache build is running (EKOM_SEED_STARTUP_MODE=create adds N
/// new nodes instead) and logs whether the catalog recovers.
/// </summary>
public sealed class PerformanceDataSeederComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("EKOM_SEED_PERF_DATA") == "1")
        {
            builder.Services.AddHostedService<PerformanceDataSeeder>();
        }
    }
}

internal sealed class PerformanceDataSeeder : BackgroundService
{
    private static readonly string[] Languages = ["en-US", "is-IS"];

    private static readonly FieldDefinition[] Fields =
    [
        new("Color", "color", true, true, false, ["Black", "White", "Red", "Blue", "Green", "Yellow", "Pink", "Grey", "Brown", "Orange", "Purple", "Beige"]),
        new("Size", "size", true, true, false, ["XS", "S", "M", "L", "XL", "XXL", "36", "38", "40", "42"]),
        new("Brand", "brand", true, false, false, Enumerable.Range(1, 40).Select(i => $"Brand {i:00}").ToArray()),
        new("Material", "material", true, true, false, ["Cotton", "Wool", "Polyester", "Linen", "Silk", "Leather", "Denim", "Viscose", "Nylon", "Cashmere"]),
        new("Season", "season", true, false, false, ["Spring", "Summer", "Autumn", "Winter"]),
        new("Gender", "gender", true, false, false, ["Women", "Men", "Unisex"]),
        new("Collection", "collection", true, false, false, Enumerable.Range(1, 25).Select(i => $"Collection {i:00}").ToArray()),
        new("Features", "features", true, true, true, ["Waterproof", "Breathable", "Recycled", "Organic", "Stretch", "Lined", "Reflective", "Windproof"]),
        new("Care instructions", "careInstructions", false, false, false, []),
        new("Weight", "weight", false, false, false, []),
    ];

    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<PerformanceDataSeeder> _logger;

    public PerformanceDataSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IHostApplicationLifetime lifetime,
        ILogger<PerformanceDataSeeder> logger)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);

        try
        {
            // Diagnostic mode: publish straight away, while Ekom's startup cache build is running.
            if (int.TryParse(Environment.GetEnvironmentVariable("EKOM_SEED_REPUBLISH_AT_STARTUP"), out var republish))
            {
                await Task.Run(() => RepublishAtStartup(republish, stoppingToken), stoppingToken);

                // Report whether the catalog recovers once the publishing stops.
                var recovery = System.Diagnostics.Stopwatch.StartNew();
                while (!EkomCachesReady() && recovery.Elapsed < TimeSpan.FromSeconds(60))
                    await Task.Delay(250, stoppingToken);
                _logger.LogInformation("Seeder: Ekom caches ready = {Ready} {Elapsed} ms after publishing stopped", EkomCachesReady(), recovery.ElapsedMilliseconds);
                return;
            }

            // Ekom fills its caches in the background after startup. Publishing content while
            // that build is running blocks both, so wait until products are being served.
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (!EkomCachesReady())
            {
                if (waited.Elapsed > TimeSpan.FromMinutes(5))
                    throw new TimeoutException("Ekom caches were not ready after 5 minutes.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            await Task.Run(() => Seed(stoppingToken), stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Performance data seeding failed");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private void RepublishAtStartup(int count, CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var productType = _contentTypeService.Get("ekmProduct") ?? throw new InvalidOperationException("ekmProduct not found");
        if (Environment.GetEnvironmentVariable("EKOM_SEED_STARTUP_MODE") == "create")
        {
            // New nodes under the Metafields folder, as the first seeding run did when it hung.
            var folderType = _contentTypeService.Get("ekmMetafields") ?? throw new InvalidOperationException("ekmMetafields not found");
            var folder = _contentService.GetPagedOfType(folderType.Id, 0, 10, out _, null).First(x => !x.Trashed);
            for (var i = 1; i <= count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var content = _contentService.Create($"Startup test {i}", folder.Id, "ekmMetafield");
                content.SetValue("title", PerLanguage($"Startup test {i}"));
                content.SetValue("alias", $"startupTest{i}");
                content.SetValue("description", "Startup publish test");
                content.SetValue("filterable", true);
                content.SetValue("enableMultipleChoice", true);
                content.SetValue("allConditionsMustMatch", false);
                content.SetValue("required", false);
                content.SetValue("readOnly", false);
                content.SetValue("values", "[{\"id\":\"a\",\"values\":{\"en-US\":\"A\",\"is-IS\":\"A\"}}]");
                SaveAndPublish(content);
                _logger.LogInformation("Seeder: created node {Count} after {Elapsed} ms", i, watch.ElapsedMilliseconds);
            }

            return;
        }

        var products = _contentService.GetPagedOfType(productType.Id, 0, count + 20, out _, null)
            .Where(x => x.Published && !x.Trashed && x.Name?.StartsWith("Cache product", StringComparison.Ordinal) == true)
            .Take(count)
            .ToList();
        _logger.LogInformation("Seeder: republishing {Count} products during startup", products.Count);
        for (var i = 0; i < products.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            SaveAndPublish(products[i]);
            if ((i + 1) % 10 == 0)
                _logger.LogInformation("Seeder: republished {Count} products after {Elapsed} ms", i + 1, watch.ElapsedMilliseconds);
        }

        _logger.LogInformation("Seeder: republish finished in {Elapsed} ms", watch.ElapsedMilliseconds);
    }

    private static bool EkomCachesReady()
    {
        try
        {
            return Ekom.API.Store.Instance.GetAllStores()
                .Any(store => Ekom.API.Catalog.Instance.GetAllProducts(store.Alias).TotalProductCount > 0);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void Seed(CancellationToken ct)
    {
        var targetProducts = int.TryParse(Environment.GetEnvironmentVariable("EKOM_SEED_PRODUCTS"), out var parsed) ? parsed : 3000;
        var fields = EnsureMetafields();

        var productType = _contentTypeService.Get("ekmProduct") ?? throw new InvalidOperationException("ekmProduct not found");
        var products = _contentService
            .GetPagedOfType(productType.Id, 0, int.MaxValue, out _, null)
            .Where(x => !x.Trashed && x.Published)
            .OrderBy(x => x.Id)
            .ToList();

        var updated = 0;
        for (var i = 0; i < products.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var product = products[i];
            if (!string.IsNullOrWhiteSpace(product.GetValue<string>("metafields")))
                continue;

            product.SetValue("metafields", BuildMetafields(fields, i));
            if (!TrySaveAndPublish(product))
            {
                // Under an unpublished category; the saved draft is enough.
                continue;
            }

            if (++updated % 250 == 0)
                _logger.LogInformation("Seeder: added metafields to {Count} existing products", updated);
        }

        var parents = products
            .Where(x => x.Name?.StartsWith("Cache product", StringComparison.Ordinal) == true)
            .Select(x => x.ParentId)
            .Distinct()
            .OrderBy(x => x)
            .Where(x => _contentService.GetById(x)?.Published == true)
            .ToList();
        if (parents.Count == 0)
            throw new InvalidOperationException("No 'Cache product' categories found to place new products in.");

        var existingPerf = products.Count(x => x.Name?.StartsWith("Perf product", StringComparison.Ordinal) == true);
        var created = 0;
        for (var number = existingPerf + 1; products.Count + created < targetProducts; number++)
        {
            ct.ThrowIfCancellationRequested();
            var index = products.Count + created;
            var name = $"Perf product {number:0000}";
            var product = _contentService.Create(name, parents[number % parents.Count], "ekmProduct");
            var price = 1000 + number * 37 % 40000;
            product.SetValue("title", PerLanguage(name));
            product.SetValue("slug", PerLanguage($"perf-product-{number:0000}"));
            product.SetValue("description", PerLanguage($"Generated performance test product {number}"));
            product.SetValue("sku", $"PERF-{number:0000}");
            product.SetValue("price", JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["Store2"] = new object[]
                {
                    new { Currency = "is-IS", Price = (decimal)price },
                    new { Currency = "en-US", Price = Math.Round(price / 100m, 2) },
                },
            }));
            product.SetValue("metafields", BuildMetafields(fields, index));
            SaveAndPublish(product);
            if (++created % 250 == 0)
                _logger.LogInformation("Seeder: created {Count} products", created);
        }

        _logger.LogInformation(
            "Seeder finished: {Fields} metafields, {Updated} existing products updated, {Created} products created",
            fields.Count, updated, created);
    }

    private List<SeededField> EnsureMetafields()
    {
        var folderType = _contentTypeService.Get("ekmMetafields") ?? throw new InvalidOperationException("ekmMetafields not found");
        var fieldType = _contentTypeService.Get("ekmMetafield") ?? throw new InvalidOperationException("ekmMetafield not found");
        var folder = _contentService.GetPagedOfType(folderType.Id, 0, 10, out _, null).First(x => !x.Trashed);
        var existing = _contentService.GetPagedOfType(fieldType.Id, 0, int.MaxValue, out _, null)
            .Where(x => !x.Trashed && x.Name != null)
            .GroupBy(x => x.Name!)
            .ToDictionary(x => x.Key, x => x.First());

        var result = new List<SeededField>();
        foreach (var definition in Fields)
        {
            if (!existing.TryGetValue(definition.Name, out var content))
            {
                var values = definition.Values
                    .Select((value, i) => new SeededValue(
                        $"{definition.Alias}-{i + 1}",
                        Languages.ToDictionary(x => x, x => x == "en-US" ? value : $"{value} (IS)")))
                    .ToList();

                content = _contentService.Create(definition.Name, folder.Id, "ekmMetafield");
                content.SetValue("title", PerLanguage(definition.Name));
                content.SetValue("alias", definition.Alias);
                content.SetValue("description", $"Performance test metafield {definition.Name}");
                content.SetValue("filterable", definition.Filterable);
                content.SetValue("enableMultipleChoice", definition.Multiple);
                content.SetValue("allConditionsMustMatch", definition.AllMustMatch);
                content.SetValue("required", false);
                content.SetValue("readOnly", false);
                content.SetValue("values", values.Count == 0
                    ? string.Empty
                    : JsonSerializer.Serialize(values.Select(x => new { id = x.Id, values = x.Values })));
                SaveAndPublish(content);
            }

            var stored = content.GetValue<string>("values");
            var seededValues = string.IsNullOrWhiteSpace(stored)
                ? []
                : JsonSerializer.Deserialize<List<SeededValue>>(stored, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            result.Add(new SeededField(content.Key, definition, seededValues));
        }

        return result;
    }

    private static string BuildMetafields(List<SeededField> fields, int index)
    {
        var random = new Random(7919 + index);
        var items = new List<object>();
        foreach (var field in fields)
        {
            if (field.Values.Count == 0)
            {
                var text = field.Definition.Alias == "weight"
                    ? (100 + random.Next(0, 1900)).ToString(CultureInfo.InvariantCulture) + " g"
                    : "Machine wash at " + (random.Next(0, 2) == 0 ? "30" : "40") + " degrees";
                items.Add(new { key = field.Key, values = (object)text });
                continue;
            }

            // Leave some products without a value so facets have realistic gaps.
            if (field.Definition.Alias is "collection" or "features" && random.Next(0, 10) < 3)
                continue;

            var count = field.Definition.Multiple ? 1 + random.Next(0, Math.Min(3, field.Values.Count)) : 1;
            var picked = field.Values.OrderBy(_ => random.Next()).Take(count).ToList();
            items.Add(new
            {
                key = field.Key,
                values = (object)picked.Select(x => new { id = x.Id, values = x.Values }).ToList(),
            });
        }

        return JsonSerializer.Serialize(items);
    }

    private static string PerLanguage(string value)
        => JsonSerializer.Serialize(new
        {
            values = Languages.ToDictionary(x => x, _ => value),
            type = "Language",
        });

    private bool TrySaveAndPublish(IContent content)
    {
        var saved = _contentService.Save(content);
        if (!saved.Success)
            throw new InvalidOperationException($"Could not save {content.Name}: {saved.Result}");

        return _contentService.Publish(content, ["*"]).Success;
    }

    private void SaveAndPublish(IContent content)
    {
        var saved = _contentService.Save(content);
        if (!saved.Success)
            throw new InvalidOperationException($"Could not save {content.Name}: {saved.Result}");

        var published = _contentService.Publish(content, ["*"]);
        if (!published.Success)
            throw new InvalidOperationException($"Could not publish {content.Name}: {published.Result}");
    }

    private sealed record FieldDefinition(string Name, string Alias, bool Filterable, bool Multiple, bool AllMustMatch, string[] Values);

    private sealed record SeededValue(string Id, Dictionary<string, string> Values);

    private sealed record SeededField(Guid Key, FieldDefinition Definition, List<SeededValue> Values);
}
