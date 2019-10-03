using Newtonsoft.Json;
using Our.Umbraco.Vorto.Models;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;

namespace Ekom.Utilities
{
    public static class ContentExtensions
    {
        public static void SetVortoValue(this IContent content, string alias, Dictionary<string, object> items)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias == alias);

            if (property != null)
            {
                var dts = Current.Services.DataTypeService;

                var dt = dts.GetByEditorAlias(property.PropertyType.PropertyEditorAlias);

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
                }
                else
                {
                    vortoItems.Add(storeAlias, value);
                }

                SetVortoValue(content, alias, vortoItems);
            }
        }

        public static VortoValue GetVortoObject(this IContent content, string alias)
        {
            var property = content.Properties.FirstOrDefault(x => x.Alias == alias);

            if (property?.GetValue() != null)
            {
                return JsonConvert.DeserializeObject<VortoValue>(property.GetValue().ToString());
            }

            return null;
        }
    }
}
