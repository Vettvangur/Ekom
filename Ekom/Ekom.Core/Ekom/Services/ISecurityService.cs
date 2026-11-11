namespace Ekom.Services
{
    public interface ISecurityService
    {
        IReadOnlyCollection<string> GetUmbracoUserGroups();

        bool IsCurrentUserAdmin();

        bool IsAuthenticated() => false;

        string? GetAuthenticatedUserName() => null;

        /// <summary>Content access with update permission, without requiring publish permission.</summary>
        bool CanEditContent(Ekom.Models.UmbracoContent content) => false;

        /// <summary>Live prices require both content edit and publish permission.</summary>
        bool CanEditPrices(Ekom.Models.UmbracoContent content) => false;
    }
}
