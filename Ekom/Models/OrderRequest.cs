using Ekom.Utilities;
using System;
using System.Collections.Generic;

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

    public class OrderMultipleRequest
    {
        public Guid productId { get; set; }
        public ICollection<Var> variant { get; set; }
        public string storeAlias { get; set; }
        public OrderAction? action { get; set; }
    }

    public class Var
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
    }
}
