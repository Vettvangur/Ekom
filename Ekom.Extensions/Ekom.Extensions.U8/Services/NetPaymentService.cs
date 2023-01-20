using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.Web.Composing;

namespace Ekom.Extensions.Services
{
    internal class NetPaymentService : INetPaymentService
    {
        public void OnSuccess(Guid paymentProviderKey, string paymentProviderName, string memberId, string custom)
        {
            int.TryParse(memberId, out var memberID);

            LocalCallback.OnSuccess(new Umbraco.NetPayment.OrderStatus
            {
                Member = memberID,
                PaymentProviderKey = paymentProviderKey,
                PaymentProvider = paymentProviderName,
                Custom = custom,
            });
        }

        public async Task<string> ProcessPaymentAsync(IPaymentProvider ekomPP, IOrderInfo order, string orderTitle)
        {
            var orderItems = new List<OrderItem>();
            orderItems.Add(new OrderItem
            {
                GrandTotal = order.ChargedAmount.Value,
                Price = order.ChargedAmount.Value,
                Title = orderTitle,
                Quantity = 1,
            });

            var pp = NetPayment.Instance.GetPaymentProvider(ekomPP.Name);

            var language = !string.IsNullOrEmpty(ekomPP.GetPropertyValue("language", order.StoreInfo.Alias)) ? ekomPP.GetPropertyValue("language", order.StoreInfo.Alias) : "IS";

            return await pp.RequestAsync(new PaymentSettings
            {
                CustomerInfo = new Umbraco.NetPayment.CustomerInfo()
                {
                    Address = order.CustomerInformation.Customer.Address,
                    City = order.CustomerInformation.Customer.City,
                    Email = order.CustomerInformation.Customer.Email,
                    Name = order.CustomerInformation.Customer.Name,
                    NationalRegistryId = order.CustomerInformation.Customer.Properties.GetPropertyValue("customerSsn"),
                    PhoneNumber = order.CustomerInformation.Customer.Phone,
                    PostalCode = order.CustomerInformation.Customer.ZipCode
                },
                Orders = orderItems,
                SkipReceipt = true,
                VortoLanguage = order.StoreInfo.Alias,
                Language = language,
                Member = Current.UmbracoHelper.MembershipHelper.GetCurrentMemberId(),
                OrderCustomString = order.UniqueId.ToString(),
                //paymentProviderId: paymentRequest.PaymentProvider.ToString()
            }).ConfigureAwait(false);
        }
    }
}
