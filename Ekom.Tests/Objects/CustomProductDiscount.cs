using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ekom.Tests.Objects
{
    public class CustomProductDiscount : GlobalDiscount
    {
        public CustomProductDiscount(IStore store, string json) : base(store)
        {
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }
}
