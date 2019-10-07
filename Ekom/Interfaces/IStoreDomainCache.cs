using Ekom.Cache;
using System;
using System.Collections.Concurrent;
using Umbraco.Core.Models;

namespace Ekom.Interfaces
{
    interface IStoreDomainCache : IBaseCache<IDomain>
    {
        void AddReplace(IDomain domain);
    }
}
