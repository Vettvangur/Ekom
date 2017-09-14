using System;
using uWebshop.Helpers;

namespace uWebshop.Models
{
    public class OrderRequest
    {
        public Guid productId { get; set; }
        public Guid? variantId { get; set; }
        public string storeAlias { get; set; }
        public int quantity { get; set; }
        public OrderAction? action { get; set; }
    }
}
