using log4net;
using Newtonsoft.Json;
using Our.Umbraco.Vorto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Ekom.Utilities
{
    public static class ContentExtensions
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static void SetVortoValue(this IContent content, string alias, Dictionary<string,object> items)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias == alias);

            if (property != null)
            {

                var vortoValue = new VortoValue();
                vortoValue.DtdGuid = property.Key;
                vortoValue.Values = items;

                var json = JsonConvert.SerializeObject(vortoValue);

                content.SetValue(alias, json);

            }
        }
    }
}
