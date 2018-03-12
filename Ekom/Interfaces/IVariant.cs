using Ekom.Models;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface IVariant : INodeEntity
    {
        IPrice Price { get; }
        IProduct Product { get; }
        int ProductId { get; }
        Guid ProductKey { get; }
        string SKU { get; }
        int Stock { get; }
        IStore Store { get; }
        IVariantGroup VariantGroup { get; }
        Guid VariantGroupKey { get; }

        List<ICategory> Categories();
    }
}
