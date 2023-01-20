using Ekom.Models;

namespace Ekom.Cache
{
    interface IStoreDomainCache : IBaseCache<UmbracoDomain>
    {
        void AddReplace(UmbracoDomain domain);
    }
}
