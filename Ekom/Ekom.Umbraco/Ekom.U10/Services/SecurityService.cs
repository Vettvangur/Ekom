using Ekom.Services;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

class SecurityService : ISecurityService
{
    readonly BackofficeUserAccessor _backofficeUserAccessor;
    readonly IUserService _userService;

    public SecurityService(BackofficeUserAccessor backofficeUserAccessor, IUserService userService)
    {
        _backofficeUserAccessor = backofficeUserAccessor;
        _userService = userService;
    }

    public IReadOnlyCollection<string> GetUmbracoUserGroups()
    {
        return GetCurrentUserGroupAliases();
    }

    public bool IsCurrentUserAdmin()
    {
        return GetCurrentUserGroupAliases().Contains(Constants.Security.AdminGroupAlias, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyCollection<string> GetCurrentUserGroupAliases()
    {
        var userTicket = _backofficeUserAccessor.BackofficeUser;

        if (!userTicket.IsAuthenticated)
        {
            return Array.Empty<string>();
        }

        var user = _userService.GetByUsername(userTicket.GetUserName());

        if (user == null)
        {
            return Array.Empty<string>();
        }

        return user.Groups
            .Select(x => x.Alias)
            .ToArray();
    }
}
