using Ekom.Utilities;

namespace Ekom.Models
{
    public class OrderRequest
    {
        public Guid productId { get; set; }
        public Guid? variantId { get; set; }
        public string storeAlias { get; set; }
        public int quantity { get; set; }
        public OrderAction? action { get; set; }
    }

    public class OrderlineRequest
    {
        public Guid lineId { get; set; }
        public string storeAlias { get; set; }
        [MinimumValue(0)]
        public int quantity { get; set; }
    }

    public class CouponRequest
    {
        public string coupon { get; set; }
        public string storeAlias { get; set; }
    }
}
