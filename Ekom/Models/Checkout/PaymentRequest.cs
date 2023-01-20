using System;

namespace Ekom.Models
{
    /// <summary>
    /// Unfinished
    /// </summary>
    public class PaymentRequest
    {
        public Guid PaymentProvider { get; set; }

        public Guid ShippingProvider { get; set; }
        public string CustomerEmail { get; set; }
        public string CardNumber { get; set; } = "";
        public string CVV { get; set; } = "";
        public string Expiry { get; set; } = "";
    }
}
