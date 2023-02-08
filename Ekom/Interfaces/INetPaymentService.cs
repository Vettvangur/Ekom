using Ekom.Models;

namespace Ekom.Interfaces
{
    public interface INetPaymentService
    {
        Task<string> ProcessPaymentAsync(
            IPaymentProvider pp,
            IOrderInfo order,
            string orderTitle);

        void OnSuccess(
            Guid paymentProviderKey,
            string paymentProviderName,
            string memberId,
            string custom);
    }
}
