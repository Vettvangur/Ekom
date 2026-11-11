using Ekom.Services;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

class SecurityService : ISecurityService
{
    readonly BackofficeUserAccessor _backofficeUserAccessor;
    readonly IUserService _userService;
    private Umbraco.Cms.Core.Models.Membership.IUser? _currentUser;
    private string? _currentUserName;
    private bool _currentUserResolved;

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

    public bool IsAuthenticated() => GetCurrentUser() is { IsApproved: true, IsLockedOut: false };

    public bool CanEditContent(Ekom.Models.UmbracoContent content) => CanEdit(content, false);

    public bool CanEditPrices(Ekom.Models.UmbracoContent content) => CanEdit(content, true);

    private bool CanEdit(Ekom.Models.UmbracoContent content, bool requirePublish)
    {
        if (!IsAuthenticated()) return false;
        var ticket = _backofficeUserAccessor.BackofficeUser;
        var user = GetCurrentUser();
        if (user == null) return false;
        if (IsCurrentUserAdmin()) return true;
        if (!ticket.GetAllowedApplications().Contains("content")) return false;
        var path = content.Path.Split(',').ToHashSet(StringComparer.Ordinal);
        if (!ticket.GetStartContentNodes().Any(id => id == -1 || path.Contains(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))) return false;
        var permissions = _userService.GetPermissionsForPath(user, content.Path).GetAllPermissions().ToHashSet(StringComparer.Ordinal);
        return permissions.Contains(Umbraco.Cms.Core.Actions.ActionUpdate.ActionLetter.ToString())
            && (!requirePublish || permissions.Contains(Umbraco.Cms.Core.Actions.ActionPublish.ActionLetter.ToString()));
    }

    private IReadOnlyCollection<string> GetCurrentUserGroupAliases()
    {
        var user = GetCurrentUser();

        if (user == null)
        {
            return Array.Empty<string>();
        }

        return user.Groups
            .Select(x => x.Alias)
            .ToArray();
    }

    /// <summary>
    /// Resolves the backoffice user once per service instance. The service is scoped, so the
    /// several permission checks made for one request share a single user lookup instead of
    /// querying the database each time. The cache is keyed by user name so a different
    /// identity is never served a previous lookup.
    /// </summary>
    private Umbraco.Cms.Core.Models.Membership.IUser? GetCurrentUser()
    {
        var ticket = _backofficeUserAccessor.BackofficeUser;

        if (!ticket.IsAuthenticated)
        {
            return null;
        }

        var userName = ticket.GetUserName();

        if (_currentUserResolved && string.Equals(_currentUserName, userName, StringComparison.Ordinal))
        {
            return _currentUser;
        }

        _currentUser = _userService.GetByUsername(userName);
        _currentUserName = userName;
        _currentUserResolved = true;

        return _currentUser;
    }
}
