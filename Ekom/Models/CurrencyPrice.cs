namespace Ekom.Models
{
    public class CurrencyPrice
    {
        public CurrencyPrice(decimal price, string currency)
        {
            Currency = currency;
            Price = price;
        }

        public string Currency { get; set; }
        public decimal Price { get; set; }
    }

    public class CurrencyPriceRoot : Dictionary<string, List<CurrencyPrice>>
    {
    }
}
