using System.Globalization;

namespace Ekom.Models.Umbraco
{
    public class UmbracoLanguage
    {
        public string IsoCode { get; set; }
        public string CultureName { get; set; }
        public CultureInfo Culture { get; set; }
    }
}
