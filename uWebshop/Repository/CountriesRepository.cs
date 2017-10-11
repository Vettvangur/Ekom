using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using uWebshop.Models;

namespace uWebshop.Domain.Repositories
{
    class CountriesRepository : ICountriesRepository
    {
        private readonly ConcurrentDictionary<string, List<Country>> _cache = new ConcurrentDictionary<string, List<Country>>();

        protected virtual string BaseXMLFileName
        {
            get { return "countries"; }
        }

        /// <summary>
        /// Gets all countries.
        /// </summary>
        /// <returns></returns>
        public List<Country> GetAllCountries()
        {
            // todo: multicurrency maybe?
            return _cache.GetOrAdd(BaseXMLFileName, s =>
            {
                // future todo: make file location configurable (web.config or through code)
                var path = HttpContext.Current.Server.MapPath($"/scripts/uWebshop/{BaseXMLFileName}.xml");

                if (!File.Exists(path))
                {
                    return DotNETFrameworkFallback();
                }

                XDocument doc;
                using (var streamReader = new StreamReader(path, new UTF8Encoding()))
                {
                    doc = XDocument.Load(streamReader);
                }

                return doc.Descendants("country").Select(country => new Country { Name = country.Value, Code = country.Attribute("code").Value }).ToList();
            });
        }

        /// <summary>
        /// Dots the net framework fallback.
        /// </summary>
        /// <returns></returns>
        protected virtual List<Country> DotNETFrameworkFallback()
        {
            var cultureList = new Dictionary<string, string>();

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                var region = new RegionInfo(culture.LCID);

                if (!(cultureList.ContainsKey(region.TwoLetterISORegionName)))
                {
                    cultureList.Add(region.TwoLetterISORegionName, region.DisplayName);
                }
            }

            return cultureList.Select(culture => new Country { Name = culture.Value, Code = culture.Key }).Where(country => !string.IsNullOrEmpty(country.Name)).OrderBy(country => country.Name).ToList();
        }
    }
}
