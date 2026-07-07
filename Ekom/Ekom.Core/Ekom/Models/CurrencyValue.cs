namespace Ekom.Models;

public class CurrencyValue
{


    public CurrencyValue(decimal v, string currency)
    {
        Value = v;
        Currency = currency;
    }

    public decimal Value { get; set; }
    public string Currency { get; set; }
}
