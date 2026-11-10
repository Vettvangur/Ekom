using Ekom.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class SecurityService : ISecurityService
{
    private readonly BackofficeUserAccessor _backofficeUserAccessor;
    private readonly IUserService _userService;

#if UMBRACO_18
    private readonly IEntityService _entityService;
    private readonly Umbraco.Cms.Core.Cache.AppCaches _appCaches;
#endif

    public SecurityService(BackofficeUserAccessor backofficeUserAccessor, IUserService userService
#if UMBRACO_18
        , IEntityService entityService, Umbraco.Cms.Core.Cache.AppCaches appCaches
#endif
    )
    {
        _backofficeUserAccessor = backofficeUserAccessor;
        _userService = userService;
#if UMBRACO_18
        _entityService = entityService;
        _appCaches = appCaches;
#endif
    }

    public IReadOnlyCollection<string> GetUmbracoUserGroups() => GetCurrentUserGroupAliases();

    public bool IsCurrentUserAdmin() => GetCurrentUserGroupAliases()
        .Contains(Constants.Security.AdminGroupAlias, StringComparer.OrdinalIgnoreCase);

    public bool IsAuthenticated() => _backofficeUserAccessor.BackofficeUser.IsAuthenticated
        && _userService.GetByUsername(_backofficeUserAccessor.BackofficeUser.GetUserName()) is { IsApproved: true, IsLockedOut: false };

    public string? GetAuthenticatedUserName() => IsAuthenticated() ? _backofficeUserAccessor.BackofficeUser.GetUserName() : null;

    public bool CanEditContent(Ekom.Models.UmbracoContent content) => CanEdit(content, false);

    public bool CanEditPrices(Ekom.Models.UmbracoContent content) => CanEdit(content, true);

    private bool CanEdit(Ekom.Models.UmbracoContent content, bool requirePublish)
    {
        if (!IsAuthenticated()) return false;
        var ticket = _backofficeUserAccessor.BackofficeUser;
        var user = _userService.GetByUsername(ticket.GetUserName());
        if (user == null) return false;
        if (IsCurrentUserAdmin()) return true;
#if UMBRACO_18
        if (!user.AllowedSections.Contains("content")) return false;
        var startNodes = Umbraco.Cms.Core.Models.UserExtensions.CalculateContentStartNodeIds(user, _entityService, _appCaches) ?? [];
#else
        if (!ticket.GetAllowedApplications().Contains("content")) return false;
        var startNodes = ticket.GetStartContentNodes();
#endif
        var path = content.Path.Split(',').ToHashSet(StringComparer.Ordinal);
        if (!startNodes.Any(id => id == -1 || path.Contains(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))) return false;
        var permissions = _userService.GetPermissionsForPath(user, content.Path).GetAllPermissions().ToHashSet(StringComparer.Ordinal);
        return (permissions.Contains(Umbraco.Cms.Core.Actions.ActionUpdate.ActionLetter.ToString()) || permissions.Contains(Umbraco.Cms.Core.Actions.ActionUpdate.ActionAlias))
            && (!requirePublish || permissions.Contains(Umbraco.Cms.Core.Actions.ActionPublish.ActionLetter.ToString()) || permissions.Contains(Umbraco.Cms.Core.Actions.ActionPublish.ActionAlias));
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
