using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Extensions.Models
{
    public class StockError
    {
        /// <summary>
        /// Product / Variant
        /// </summary>
        public bool IsVariant { get; set; }

        public Guid OrderLineKey { get; set; }

        public Exception Exception { get; set; }
    }
}
