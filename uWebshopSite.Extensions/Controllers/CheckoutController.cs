using Umbraco.Web.Mvc;

namespace uWebshopSite.Extensions.Controllers
{
    public class CheckoutController : SurfaceController
    {
        public object Pay(PaymentRequest paymentRequest)
        {
            return paymentRequest.PaymentProvider;
        }
    }

    public class PaymentRequest
    {
        public string PaymentProvider { get; set; }

        public string ShippingProvider { get; set; }
    }
}
