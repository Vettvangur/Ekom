using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class Customer
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string ZipCode { get; set; }
        public string Phone { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
