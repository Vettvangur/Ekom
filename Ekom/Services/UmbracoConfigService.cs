using Ekom.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Ekom.Services
{
    //public interface IUmbracoConfigService
    //{
    //    XDocument GetUmbracoSettingsFile();
    //    string GetUrlMode();
    //    string GetUrlModeCached();
    //}

    //public class UmbracoConfigService
    //{
    //    private readonly IHostEnvironment _hostingEnvironment;
    //    private readonly IMemoryCache _memoryCache;

    //    public UmbracoConfigService(IHostEnvironment hostingEnvironment, IMemoryCache memoryCache)
    //    {
    //        _hostingEnvironment = hostingEnvironment;
    //        _memoryCache = memoryCache;
    //    }

    //    public XDocument GetUmbracoSettingsFile()
    //    {
    //        var fileName = _hostingEnvironment.ContentRootPath + "/config/umbracoSettings.config";

    //        return XDocument.Load(fileName, LoadOptions.PreserveWhitespace);
    //    }

    //    public string GetUrlMode()
    //    {
    //        try
    //        {
    //            var settingsFile = GetUmbracoSettingsFile();

    //            if (settingsFile.Root != null)
    //            {
    //                var webRouting = settingsFile.Root.DescendantsAndSelf("web.routing").Single();

    //                if (webRouting != null)
    //                {
    //                    var urlAttr = webRouting.Attribute("urlProviderMode");

    //                    if (urlAttr != null && !string.IsNullOrEmpty(urlAttr.Value))
    //                    {
    //                        _memoryCache.Set("ekmUrlMode", urlAttr.Value);

    //                        return urlAttr.Value;

    //                        //if (Enum.TryParse(urlAttr.Value, out UrlMode mode))
    //                        //{
    //                        //    return mode;
    //                        //}
    //                    }
    //                }

    //            }
    //        }
    //        catch
    //        {

    //        }

    //        return "Relative"; // UrlMode.Relative;
    //    }

    //    public string GetUrlModeCached()
    //    {
    //        return _memoryCache.GetOrCreate("ekmUrlMode", entry => GetUrlMode());
    //    }
    //}
}
