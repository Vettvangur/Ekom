using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class CurrencyPrice
    {
        public CurrencyPrice(decimal price, string currency)
        {
            Currency = currency;
            Price = price;
        }

        public string Currency { get; set; }
        public decimal Price { get; set; }
    }
}
