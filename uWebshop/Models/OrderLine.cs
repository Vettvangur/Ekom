using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class OrderLine : IOrderLine
    {
        private Guid _productId;
        private IEnumerable<Guid> _variantIds;
        private Store _store;
        private StoreInfo _storeInfo;

        public Guid Id { get; set; }
        public OrderedProduct Product { get; set; }
        public int Quantity { get; set; }

        public OrderLine(Guid lineId, int quantity, string productJson, StoreInfo storeInfo)
        {
            Id = lineId;
            Quantity = quantity;
            _storeInfo = storeInfo;
            Product = new OrderedProduct(productJson, storeInfo);
        }

        public OrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, Guid lineId, Store store)
        {
            _productId = productId;
            _variantIds = variantIds;
            Quantity = quantity;
            Id = lineId;
            Product = new OrderedProduct(productId, variantIds, store);
        }


    }
}
