using Examine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ekom.Tests.Objects
{
    class CustomSearchResult : SearchResult
    {
        public CustomSearchResult(string json) : base()
        {
            Fields = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }
}
