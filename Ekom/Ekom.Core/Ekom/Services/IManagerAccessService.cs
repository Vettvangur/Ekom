using Ekom.Models;

namespace Ekom.Services;

public interface IManagerAccessService
{
    bool CanAccessManager();

    bool CanAccessStore(string? storeAlias);

    IReadOnlyCollection<string> GetAllowedStoreAliases();

    IEnumerable<IStore> GetAllowedStores();
}
