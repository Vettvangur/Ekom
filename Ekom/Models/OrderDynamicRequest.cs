using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class OrderDynamicRequest
    {
        public Guid OrderLineLink { get; set; }
        public decimal Price { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
    }
}
