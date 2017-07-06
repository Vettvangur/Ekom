using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class OrderedVariant
    {
        public IVariant Variant { get; set; }
        public OrderedVariant(Guid productId, Guid variantId)
        {
            Variant = API.Catalog.GetVariant(variantId);
        }
    }
}
