﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class PaymentProviderCache : PerStoreCache<PaymentProvider, PaymentProviderCache>
    {
        protected override string nodeAlias { get; } = "uwbsPaymentProvidedr";

        protected override PaymentProvider New(SearchResult r, Store store)
        {
            return new PaymentProvider(r, store);
        }
    }
}