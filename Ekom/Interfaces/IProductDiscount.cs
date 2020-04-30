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
    public interface IProductDiscount : IPerStoreNodeEntity, ICloneable
    {
        string Title { get; }
        DiscountType Type { get; }
        decimal Discount { get; }
        List<CurrencyValue> Discounts { get; }
        List<Guid> DiscountItems { get; }
        decimal StartOfRange { get; }
        decimal EndOfRange { get; }
        bool Disabled { get; }
    }
}
