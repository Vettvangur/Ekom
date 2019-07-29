using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Models
{
    public class ProductDiscount : PerStoreNodeEntity, IProductDiscount
    {

        UmbracoHelper UmbHelper => Current.Factory.GetInstance<UmbracoHelper>();
        IContent node;
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
        public virtual DiscountType Type
        {
            get
            {
                var typeValue = Properties.GetPropertyValue("type");

                if (int.TryParse(typeValue, out int typeValueInt))
                {

                    typeValue = UmbHelper.GetPreValueAsString(typeValueInt);
                }

                switch (typeValue)
                {
                    case "Fixed":
                        return DiscountType.Fixed;

                    case "Percentage":
                        return DiscountType.Percentage;
                    default:
                        return DiscountType.Fixed;
                }
            }

        }
        public virtual decimal Discount
        {
            
            get
            {
                var discount = Properties.GetPropertyValue("discount", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {
                    if (Store.Alias == "IS")
                    {

                    }

                    decimal.TryParse(discount.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value);
                    value = value * 0.01M;
                    if (value > 1)
                    {
                        return value * 0.01M;
                    }
                    return value;
                }

            }

        }
       

        public List<Guid> DiscountItems
        {
            get
            {
                List<Guid> returnList = new List<Guid>();
                var nodes = Properties.GetPropertyValue("discountItems")
                    .Split(',')
                    .Select(x => UmbHelper.Content(Udi.Parse(x))).ToList();
                foreach (var node in nodes)
                {
                    if (node.ContentType.Alias == "ekmProduct" && node.Descendants().Count() == 0)
                    {
                        returnList.Add(node.Key);
                    }
                    else if (node.ContentType.Alias == "ekmProduct" && node.Descendants().Any())
                    {
                        returnList.AddRange(
                            node.Descendants()
                                .Where(x => x.ContentType.Alias == "ekmProductVariant")
                                .Select(x => x.Key));
                    }
                    else if (node.ContentType.Alias == "ekmCategory")
                    {
                        returnList.AddRange(
                            node.Descendants()
                                .Where(x => x.ContentType.Alias == "ekmProduct" 
                                    || x.ContentType.Alias == "ekmProductVariant")
                                .Select(x => x.Key));
                    }
                }
                return returnList;
            }
        }
        /// <summary>
        /// Simple <see cref="ICloneable"/> implementation using object.MemberwiseClone
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        public virtual decimal StartOfRange
        {
            get
            {
                var discount = Properties.GetPropertyValue("startOfRange", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {

                    decimal.TryParse(discount.Replace(',', '.'), out decimal value);
                    return value;
                }
            }
        }

        public virtual decimal EndOfRange
        {
            get
            {
                var discount = Properties.GetPropertyValue("endOfRange", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {
                    decimal.TryParse(discount.Replace(',', '.'), out decimal value);
                    return value;
                }
            }
        }
        public virtual bool Disabled
        {
            get
            {
                return Properties.GetPropertyValue("disable", Store.Alias) == "1";
            }

        }
    }
    
}
