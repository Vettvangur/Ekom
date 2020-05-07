using Ekom.Models;
using Ekom.Models.Discounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace Ekom.Interfaces
{
    public interface IProductDiscount : IDiscount
    {
        decimal StartOfRange { get; }
        decimal EndOfRange { get; }
        bool Disabled { get; }
    }
}
