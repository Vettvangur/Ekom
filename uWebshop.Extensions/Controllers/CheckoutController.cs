using System.Web.Mvc;
using Umbraco.NetPayment.API;
using Umbraco.Web.Mvc;

namespace uWebshop.Extensions.Controllers
{
    public class CheckoutController : SurfaceController
    {
        public object Pay(PaymentRequest paymentRequest, FormCollection form)
        {
            var pp = NetPayment.Current.GetPaymentProvider(paymentRequest.PaymentProvider);

            return $"{paymentRequest.PaymentProvider} form: {form["customerCountry"]}";
        }
    }

    public class PaymentRequest
    {
        public string PaymentProvider { get; set; }

        public string ShippingProvider { get; set; }
    }
}
