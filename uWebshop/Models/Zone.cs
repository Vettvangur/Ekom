using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop.Models
{
    /// <summary>
    /// A grouping of countries, used to map payment providers and shipping providers to regions
    /// </summary>
    public class Zone
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public List<string> Countries { get; set; }
        public int SortOrder { get; set; }

        public Zone() : base() { }
        public Zone(SearchResult item)
        {
            Id    = item.Id;
            Title = item.Fields["nodeName"];

            var examineSortOrder = item.Fields["sortOrder"];
            if (!string.IsNullOrEmpty(examineSortOrder))
            {
                SortOrder = int.Parse(examineSortOrder);
            }

            foreach (var country in item.Fields["zone"].Split(','))
            {
                Countries.Add(country);
            }
        }
        public Zone(IContent item)
        {
            Id    = item.Id;
            Title = item.Name;

            SortOrder = item.SortOrder;

            foreach (var country in item.GetValue<string>("zone").Split(','))
            {
                Countries.Add(country);
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
