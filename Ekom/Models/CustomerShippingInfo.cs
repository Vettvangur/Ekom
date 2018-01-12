﻿using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class CustomerShippingInfo
    {
        public string Name
        {

            get
            {
                return Properties.GetPropertyValue("shippingName");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetPropertyValue("shippingAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetPropertyValue("shippingCity");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetPropertyValue("shippingCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetPropertyValue("shippingZipCode");
            }
        }


        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
