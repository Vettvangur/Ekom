using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core.Models.PublishedContent;

namespace Ekom.Interfaces
{
    public interface IConfigHelper
    {
        XDocument GetUmbracoSettingsFile();
        UrlMode GetUrlMode();
        UrlMode GetUrlModeCached();
    }
}
