using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Utilities;
using Examine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace Ekom.Models.Discounts
{
    public class ProductDiscount : Discount, IProductDiscount
    {
        /// <summary>
        /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
        /// </summary>
        /// <param name="store"></param>
        public ProductDiscount(IStore store) : base(store) { }

        /// <summary>
        /// Construct Product from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ProductDiscount(ISearchResult item, IStore store) : base(item, store)
        {
        }
        public ProductDiscount(IContent node, IStore store) : base(node, store)
        {
        }

        /// <summary>
        /// If the discount can be used with productdiscounts
        /// Doesn't apply for ProductDiscounts but allows Discount and ProductDiscount to share an interface.
        /// </summary>
        public override bool Exclusive { get; } = true;
    }
}
