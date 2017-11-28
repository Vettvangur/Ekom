using Ekom.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class OrderedShippingProvider
    {
        private ShippingProvider _provider;

        public OrderedShippingProvider(ShippingProvider provider)
        {
            this._provider = provider;
        }
        public int Id {
            get
            {
                return _provider.Id;
            }
        }
        public Guid Key {
            get
            {
                return _provider.Key;
            }
        }
        public string Title {
            get
            {
                return _provider.Title;
            }
        }

        public IDiscountedPrice Price {
           get
            {
                return _provider.Price;
            }
        }
    }
}
