using Ekom.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Ekom.Tests.Objects
{
    class CustomStore : Store
    {
        public override int StoreRootNode { get; }
        public override string Url { get; }
        public override IEnumerable<IDomain> Domains { get; }
        public CustomStore(string json, int storeRootNode)
        {
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            StoreRootNode = storeRootNode;
            Url = "http://ekom.localhost.vettvangur.is";
            Domains = Enumerable.Empty<IDomain>();
        }
    }
}
