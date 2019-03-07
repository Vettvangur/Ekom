using log4net;
using Newtonsoft.Json;
using Our.Umbraco.Vorto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using static Ekom.EkomStartup;

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
                var dts = ApplicationContext.Current.Services.DataTypeService;

                var dt = dts.GetDataTypeDefinitionByPropertyEditorAlias(property.PropertyType.PropertyEditorAlias);

                if (dt.Any())
                {
                    var dtt = dt.FirstOrDefault();

                    var vortoValue = new VortoValue();
                    vortoValue.DtdGuid = dtt.Key;
                    vortoValue.Values = items;

                    var json = JsonConvert.SerializeObject(vortoValue);

                    content.SetValue(alias, json);

                }

            }
        }

        public static void SetVortoValue(this IContent content, string alias, string storeAlias, object value)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias.ToLower() == alias.ToLower());

            if (property != null)
            {
                var vortoValue = content.GetVortoObject(alias);

                var vortoItems = new Dictionary<string, object>();

                if (vortoValue != null && vortoValue.Values != null && vortoValue.Values.Any())
                {
                    foreach (var vvalue in vortoValue.Values)
                    {
                        var val = vvalue.Key.ToLower() == storeAlias.ToLower() ? value : vvalue.Value;

                        vortoItems.Add(vvalue.Key, val);
                    }
                } else
                {
                    vortoItems.Add(storeAlias, value);
                }

                SetVortoValue(content, alias, vortoItems);

            }
        }

        public static VortoValue GetVortoObject(this IContent content, string alias)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias == alias);

            if (property != null && property.Value != null)
            {
                return JsonConvert.DeserializeObject<VortoValue>(property.Value.ToString());
            }

            return null;
        }
    }
}
