using Ekom.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class SecurityService : ISecurityService
{
    private readonly BackofficeUserAccessor _backofficeUserAccessor;
    private readonly IUserService _userService;

    public SecurityService(BackofficeUserAccessor backofficeUserAccessor, IUserService userService)
    {
        _backofficeUserAccessor = backofficeUserAccessor;
        _userService = userService;
    }

    public IReadOnlyCollection<string> GetUmbracoUserGroups() => GetCurrentUserGroupAliases();

    public bool IsCurrentUserAdmin() => GetCurrentUserGroupAliases()
        .Contains(Constants.Security.AdminGroupAlias, StringComparer.OrdinalIgnoreCase);

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
