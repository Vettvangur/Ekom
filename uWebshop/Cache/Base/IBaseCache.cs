using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Cache
{
    public interface IBaseCache<T> : ICache
    {
        ConcurrentDictionary<int, T> Cache { get; }
    }
}
