using Ekom.Interfaces;
using Newtonsoft.Json.Linq;
using System;

namespace Ekom.Models
{
    class OrderedPrice : IPrice
    {
        public OrderedPrice(IPrice price)
        {

        }

        public OrderedPrice(JToken orderedPriceObj)
        {

        }

        public decimal OriginalValue => throw new NotImplementedException();

        public ICalculatedPrice BeforeDiscount => throw new NotImplementedException();

        public decimal Value => throw new NotImplementedException();

        public IDiscount Discount => throw new NotImplementedException();

        public ICalculatedPrice WithVat => throw new NotImplementedException();

        public ICalculatedPrice WithoutVat => throw new NotImplementedException();

        public decimal Vat => throw new NotImplementedException();

        public object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
