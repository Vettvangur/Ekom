using Ekom.Models;

namespace Ekom.Services;

public interface IWarehouseDefinitionService
{
    IReadOnlyList<WarehouseDefinition> GetPublishedWarehouses(string storeAlias);
}
