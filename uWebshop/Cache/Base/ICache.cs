using Examine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace uWebshop.Cache
{
    /// <summary>
    /// Caches implementing this interface offer addition and removal methods
    /// for usage by umbraco event listeners
    /// </summary>
    public interface ICache
    {
        string nodeAlias { get; }

        void AddReplace(IContent node);

        void Remove(int id);

        void FillCache();
    }
}
