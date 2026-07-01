using Ekom.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Ekom.Repositories;

public class CountriesRepository
{
    private static readonly ConcurrentDictionary<string, List<Country>> _cache = new();

    private readonly ILogger _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logFac"></param>
    public CountriesRepository(ILogger<CountriesRepository> logger)
    {
        _logger = logger;
    }

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
            IWebHostEnvironment? env = Configuration.Resolver.GetService<IWebHostEnvironment>();
            //string webRootPath = env.WebRootPath;
            string contentRootPath = env.ContentRootPath;

            string path = Path.Combine(contentRootPath, $"scripts/Ekom/{BaseXMLFileName}.xml");

            if (!File.Exists(path))
            {
                return DotNETFrameworkFallback();
            }

            XDocument doc;
            using (StreamReader streamReader = new StreamReader(path, new UTF8Encoding()))
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
        Dictionary<string, string> cultureList = new Dictionary<string, string>();

        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                RegionInfo region = new RegionInfo(culture.LCID);

                cultureList.TryAdd(region.TwoLetterISORegionName, region.DisplayName);
            }
            catch
            {
                // Culture ID 4096 (0x1000) is a neutral culture; a region cannot be created from it.  Error sometimes on some machines
            }

        }

        return cultureList
            .Select(culture => new Country { Name = culture.Value, Code = culture.Key })
            .Where(country => !string.IsNullOrEmpty(country.Name))
            .OrderBy(country => country.Name).ToList();
    }
}
