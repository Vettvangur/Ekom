using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class CurrencyValue
    {


        public CurrencyValue(decimal v, string currency)
        {
            Value = v;
            Currency = currency;
        }

        public decimal Value { get; set; }
        public string Currency { get; set; }
    }
}
