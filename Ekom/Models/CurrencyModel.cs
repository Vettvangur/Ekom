using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace Ekom.Models
{
    public class CurrencyModel
    {
        public string CurrencyFormat { get; set; }
        public string CurrencyValue { get; set; }
    }
}
