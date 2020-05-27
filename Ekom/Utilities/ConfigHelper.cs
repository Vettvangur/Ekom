using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Ekom.Interfaces;
using Umbraco.Core.Cache;
using Umbraco.Core.IO;
using Umbraco.Core.Models.PublishedContent;

namespace Ekom.Utilities
{
    public class ConfigHelper : IConfigHelper
    {
        private readonly IAppPolicyCache _runtimeCache;

        public ConfigHelper(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
        }

        public XDocument GetUmbracoSettingsFile()
        {
            var fileName = IOHelper.MapPath($"{SystemDirectories.Config}/umbracoSettings.config");

            return XDocument.Load(fileName, LoadOptions.PreserveWhitespace);
        }

        public UrlMode GetUrlMode()
        {
            try
            {
                var settingsFile = GetUmbracoSettingsFile();

                if (settingsFile.Root != null)
                {
                    var webRouting = settingsFile.Root.DescendantsAndSelf("web.routing").Single();

                    if (webRouting != null)
                    {
                        var urlAttr = webRouting.Attribute("urlProviderMode");

                        if (urlAttr != null && !string.IsNullOrEmpty(urlAttr.Value))
                        {
                            if (Enum.TryParse(urlAttr.Value, out UrlMode mode))
                            {
                                return mode;
                            }
                        }
                    }

                }
            }
            catch
            {
              
            }

            return UrlMode.Relative;
        }

        public UrlMode GetUrlModeCached()
        {
            return _runtimeCache.GetCacheItem("ekmUrlMode", GetUrlMode);
        }
    }
}
