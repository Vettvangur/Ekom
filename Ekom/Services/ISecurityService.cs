namespace Ekom.Services
{
    internal interface ISecurityService
    {
        IEnumerable<string> GetUmbracoUserGroups();
    }
}
