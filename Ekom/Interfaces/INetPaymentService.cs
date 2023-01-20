using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
