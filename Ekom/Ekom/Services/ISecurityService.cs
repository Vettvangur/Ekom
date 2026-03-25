namespace Ekom.Services
{
    public interface ISecurityService
    {
        IReadOnlyCollection<string> GetUmbracoUserGroups();

        bool IsCurrentUserAdmin();
    }
}
