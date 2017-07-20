﻿using System;
using System.Collections.Generic;
using System.Linq;
using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class PaymentProviderCache : PerStoreCache<PaymentProvider>
    {
        protected override string nodeAlias { get; } = "uwbsPaymentProvidedr";

        protected override PaymentProvider New(SearchResult r, Store store)
        {
            return new PaymentProvider(r, store);
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="storeSvc"></param>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        public PaymentProviderCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager
        )
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger(typeof(ZoneCache));
        }
    }
}
