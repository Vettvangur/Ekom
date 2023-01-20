using System.Threading.Tasks;

namespace Ekom.Services
{
    public interface IMailService
    {
        Task SendAsync(string subject, string body, string recipient = null, string sender = null);
    }
}
