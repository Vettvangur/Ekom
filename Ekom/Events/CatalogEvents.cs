using System.Globalization;

namespace Ekom.Events
{
    public static class CatalogEvents
    {
        public static event EventHandler<CurrencyStringEventArgs> CurrencyStringEvent;
        internal static void OnCurrencyString(object sender, CurrencyStringEventArgs args)
            => CurrencyStringEvent?.Invoke(sender, args);
        
    }
    public class CurrencyStringEventArgs : EventArgs
    {
        public CultureInfo CultureInfo { get; set; }
        public decimal Value { get; set; }
        public string ValueString { get; set; }
    }
}
