using System.Text.Json;
using Ekom.Models;

namespace Ekom.Services;

internal sealed class WarehouseDefinitionService : IWarehouseDefinitionService
{
    private const string WarehousesPropertyAlias = "warehouses";
    private readonly IStoreService _storeService;

    public WarehouseDefinitionService(IStoreService storeService)
    {
        _storeService = storeService;
    }

    public IReadOnlyList<WarehouseDefinition> GetPublishedWarehouses(string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            throw new ArgumentException("Store alias is required.", nameof(storeAlias));
        }

        var store = _storeService.GetStoreByAlias(storeAlias.Trim())
            ?? throw new InvalidOperationException($"Store '{storeAlias}' was not found.");
        var value = store.GetValue(WarehousesPropertyAlias);

        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<WarehouseDefinition>();
        }

        var warehouses = JsonSerializer.Deserialize<List<PersistedWarehouseDefinition>>(value, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (warehouses is null)
        {
            return Array.Empty<WarehouseDefinition>();
        }

        return warehouses
            .Where(warehouse => warehouse.Key != Guid.Empty
                && !string.IsNullOrWhiteSpace(warehouse.Code)
                && !string.IsNullOrWhiteSpace(warehouse.Name))
            .Select(warehouse => new WarehouseDefinition
            {
                Key = warehouse.Key,
                Code = warehouse.Code.Trim(),
                Name = warehouse.Name.Trim(),
                SortOrder = warehouse.SortOrder,
                Visible = warehouse.Visible ?? warehouse.Enabled ?? true,
            })
            .OrderBy(warehouse => warehouse.SortOrder)
            .ThenBy(warehouse => warehouse.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class PersistedWarehouseDefinition
    {
        public Guid Key { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public bool? Visible { get; init; }
        public bool? Enabled { get; init; }
    }
}
