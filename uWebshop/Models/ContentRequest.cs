using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Models
{
    public class ContentRequest
    {
        public Store Store { get; set; }
        public object Currency { get; set; }
        public string DomainPrefix { get; set; }
        public Product Product { get; set; }
        public Category Category { get; set; }
    }
}
