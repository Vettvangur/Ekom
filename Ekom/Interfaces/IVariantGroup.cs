using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Ekom.Interfaces
{
    public interface IVariantGroup : INodeEntity
    {
        IEnumerable<IPublishedContent> Images { get; }
        IProduct Product { get; }
        int ProductId { get; }
        Guid ProductKey { get; }
        IEnumerable<IVariant> Variants { get; }
    }
}
