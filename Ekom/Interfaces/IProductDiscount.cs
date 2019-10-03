using Ekom.Models.Discounts;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IProductDiscount : IPerStoreNodeEntity, ICloneable
    {
        DiscountType Type { get; }
        decimal Discount { get; }
        List<Guid> DiscountItems { get; }
        decimal StartOfRange { get; }
        decimal EndOfRange { get; }
        bool Disabled { get; }
    }
}
