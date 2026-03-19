using Ekom.Models;

namespace Ekom.Klaviyo.Helpers;

public static class OrderInfoExtensions
{
    public static string KlaviyoUniqueId(this IOrderInfo orderInfo)
    {
        var klaviyoUniqueId = orderInfo.CustomerInformation.Customer.Value("customerKlaviyoOrderUniqueId");

        return  !string.IsNullOrEmpty(klaviyoUniqueId) ? klaviyoUniqueId : $"{orderInfo.UniqueId}";
    }
}
