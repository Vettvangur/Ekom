namespace Ekom.Models
{
    public class CheckoutStatus
    {
        public string MemberKey { get; set; }
        public Guid PaymentProviderKey { get; set; }
        public string PaymentProvider { get; set; }
        public Guid OrderId { get; set; }
        public string SuccessUrl { get; set; }
        public string ErrorUrl { get; set; }
    }
}
