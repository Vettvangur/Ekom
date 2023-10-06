using Ekom.Models;

namespace Ekom.Services
{
    public interface IMemberService
    {
        UmbracoMember GetCurrentMember();
        UmbracoMember GetByUsername(string t);
        void Save(Dictionary<string, object> t, UmbracoMember member);
        void Save(Dictionary<string, object> t, string? userSsn);
    }
}
