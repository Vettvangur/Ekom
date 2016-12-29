﻿using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.Cache;

namespace uWebshop.Models
{
    public class Store
    {
        public int Id { get; set; }
        public string Alias { get; set; }
        public StoreNode RootNode { get; set; }
        public int StoreRootNode {get; set;}
        public int Level { get; set; }
        public IEnumerable<IDomain> Domains { get; set; }

        public Store(): base() { }
        public Store (SearchResult item)
        {
            Id               = item.Id;
            Alias            = item.Fields["nodeName"];

            StoreRootNode    = Convert.ToInt32(item.Fields["storeRootNode"]);
            Level            = Convert.ToInt32(item.Fields["level"]);

            Domains          = StoreDomainCache.Cache
                                               .Where(x => x.Value.RootContentId == StoreRootNode)
                                               .Select(x => x.Value);
        }
        public Store(IContent item)
        {
            Id = item.Id;
            Alias = item.Name;

            StoreRootNode = item.GetValue<int>("storeRootNode");
            Level = item.Level;

            Domains = StoreDomainCache.Cache
                                      .Where(x => x.Value.RootContentId == StoreRootNode)
                                      .Select(x => x.Value);

        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
