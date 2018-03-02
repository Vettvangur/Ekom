using Ekom.Interfaces;

namespace Ekom.Models.OrderedObjects
{
    class OrderedCalculatedPrice : ICalculatedPrice
    {
        public decimal Value { get; internal set; }

        public bool IsDiscounted { get; internal set; }

        public string ToCurrencyString { get; internal set; }
    }
}
