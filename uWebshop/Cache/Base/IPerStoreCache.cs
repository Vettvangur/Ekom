using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public interface IPerStoreCache
    {
        void FillCacheInternal(Store store);
    }
}
