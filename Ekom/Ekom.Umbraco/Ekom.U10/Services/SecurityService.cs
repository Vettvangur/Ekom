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

    public bool IsAuthenticated() => _backofficeUserAccessor.BackofficeUser.IsAuthenticated
        && _userService.GetByUsername(_backofficeUserAccessor.BackofficeUser.GetUserName()) is { IsApproved: true, IsLockedOut: false };

    public bool CanEditPrices(Ekom.Models.UmbracoContent content)
    {
        if (!IsAuthenticated()) return false;
        var ticket = _backofficeUserAccessor.BackofficeUser;
        var user = _userService.GetByUsername(ticket.GetUserName());
        if (user == null) return false;
        if (IsCurrentUserAdmin()) return true;
        if (!ticket.GetAllowedApplications().Contains("content")) return false;
        var path = content.Path.Split(',').ToHashSet(StringComparer.Ordinal);
        if (!ticket.GetStartContentNodes().Any(id => id == -1 || path.Contains(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))) return false;
        var permissions = _userService.GetPermissionsForPath(user, content.Path).GetAllPermissions().ToHashSet(StringComparer.Ordinal);
        return permissions.Contains(Umbraco.Cms.Core.Actions.ActionUpdate.ActionLetter.ToString())
            && permissions.Contains(Umbraco.Cms.Core.Actions.ActionPublish.ActionLetter.ToString());
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
