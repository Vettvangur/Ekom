namespace Ekom.Services
{
    public interface ISecurityService
    {
        IReadOnlyCollection<string> GetUmbracoUserGroups();

        bool IsCurrentUserAdmin();

        bool IsAuthenticated() => false;

        /// <summary>Live prices require both content edit and publish permission.</summary>
        bool CanEditPrices(Ekom.Models.UmbracoContent content) => false;
    }
}
