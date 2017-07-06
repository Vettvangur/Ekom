using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class Cart : ICart
    {
        private Guid uniqueId;

        public Cart(Guid uniqueId)
        {
            this.uniqueId = uniqueId;
        }

        public Guid UniqueId { get; set; }
        public IEnumerable<IOrderLine> OrderLines { get; set; }
        public int Quantity { get; set; }
        public StoreInfo StoreInfo { get; set; }
    }
}
