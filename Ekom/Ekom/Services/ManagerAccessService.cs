using Ekom.Models;
using Microsoft.Extensions.Options;

namespace Ekom.Services;

public sealed class ManagerAccessService : IManagerAccessService
{
    private readonly IOptions<EkomOptions> _options;
    private readonly ISecurityService _securityService;
    private readonly IStoreService _storeService;

    public ManagerAccessService(
        IOptions<EkomOptions> options,
        ISecurityService securityService,
        IStoreService storeService)
    {
        _options = options;
        _securityService = securityService;
        _storeService = storeService;
    }

    public bool CanAccessManager()
    {
        if (_securityService.IsCurrentUserAdmin())
        {
            return true;
        }

        var requiredGroups = GetManagerAccessGroups();

        if (requiredGroups.Count == 0)
        {
            return true;
        }

        var userGroups = _securityService.GetUmbracoUserGroups();

        return userGroups.Any(requiredGroups.Contains);
    }

    public bool CanAccessStore(string? storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            return false;
        }

        if (_securityService.IsCurrentUserAdmin())
        {
            return _storeService.GetAllStores().Any(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));
        }

        var normalizedStoreAlias = storeAlias.Trim();
        var permissions = _options.Value.Manager.StoreGroupPermissions;

        if (permissions.Count == 0)
        {
            return true;
        }

        if (!TryGetAllowedGroupsForStore(permissions, normalizedStoreAlias, out var allowedGroups))
        {
            return false;
        }

        if (allowedGroups.Count == 0)
        {
            return false;
        }

        var userGroups = _securityService.GetUmbracoUserGroups();

        return userGroups.Any(allowedGroups.Contains);
    }

    public IReadOnlyCollection<string> GetAllowedStoreAliases()
    {
        if (_securityService.IsCurrentUserAdmin())
        {
            return _storeService.GetAllStores()
                .Select(x => x.Alias)
                .ToArray();
        }

        return _storeService.GetAllStores()
            .Select(x => x.Alias)
            .Where(CanAccessStore)
            .ToArray();
    }

    public IEnumerable<IStore> GetAllowedStores()
    {
        if (_securityService.IsCurrentUserAdmin())
        {
            return _storeService.GetAllStores();
        }

        var permissions = _options.Value.Manager.StoreGroupPermissions;

        if (permissions.Count == 0)
        {
            return _storeService.GetAllStores();
        }

        var allowedStores = new HashSet<string>(GetAllowedStoreAliases(), StringComparer.OrdinalIgnoreCase);

        return _storeService.GetAllStores()
            .Where(x => allowedStores.Contains(x.Alias));
    }

    private HashSet<string> GetManagerAccessGroups()
    {
        var groups = ParseConfiguredGroups(_options.Value.Manager.SectionAccessGroup);

        foreach (string[] storeGroups in _options.Value.Manager.StoreGroupPermissions.Values)
        {
            groups.UnionWith(ParseConfiguredGroups(storeGroups));
        }

        return groups;
    }

    private static bool TryGetAllowedGroupsForStore(
        IDictionary<string, string[]> storeGroupPermissions,
        string storeAlias,
        out HashSet<string> allowedGroups)
    {
        foreach (var entry in storeGroupPermissions)
        {
            if (!entry.Key.Equals(storeAlias, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            allowedGroups = ParseConfiguredGroups(entry.Value);
            return true;
        }

        allowedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return false;
    }

    private static HashSet<string> ParseConfiguredGroups(IEnumerable<string>? groups)
    {
        if (groups == null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return groups
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ParseConfiguredGroups(string? groups)
    {
        if (string.IsNullOrWhiteSpace(groups))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return groups
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
