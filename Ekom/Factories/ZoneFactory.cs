using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    class ZoneFactory : IObjectFactory<IZone>
    {
        public IZone Create(ISearchResult item)
        {
            return new Zone(item);
        }

        public IZone Create(IContent item)
        {
            return new Zone(item);
        }
    }
}
