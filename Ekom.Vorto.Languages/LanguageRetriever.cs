using Ekom.API;
using Our.Umbraco.Vorto.Models;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Vorto.Languages
{
    public class LanguageRetriever : ILanguageRetriever
    {
        public IEnumerable<Language> GetLanguages()
        {
            var stores = Store.Current.GetAllStores();

            return stores.Select(x => new Language
            {
                IsoCode = x.Alias,
                Name = x.Alias,
                NativeName = x.Alias,
            });
        }
    }
}
