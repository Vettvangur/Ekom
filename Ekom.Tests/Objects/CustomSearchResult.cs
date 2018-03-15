using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
