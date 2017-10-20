using Ekom.Helpers;
using Ekom.Interfaces;

namespace Ekom.Services
{
    class PriceCalculationService : IPriceCalculationService
    {
        public int WithVat(int originalTotal, decimal vat)
        {
            return VatCalculator.WithVat(originalTotal, vat);
        }

        public int WithoutVat(int originalTotal, decimal vat)
        {
            return VatCalculator.WithoutVat(originalTotal, vat);
        }

        public int Vat(int originalTotal, decimal vat)
        {
            return VatCalculator.VatAmountFromOriginal(originalTotal, vat);
        }
    }
}
