using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Helpers;
using uWebshop.Interfaces;

namespace uWebshop.Services
{
    class PriceCalculationService : IPriceCalculationService
    {
        public int WithVat(int originalTotal, decimal vat)
        {
            return VatCalculator.WithVat(originalTotal, vat);
        }

        public int WithoutVat(int originalTotal, decimal vat)
        {
            return  VatCalculator.WithoutVat(originalTotal, vat);
        }

        public int Vat(int originalTotal, decimal vat)
        {
            return VatCalculator.VatAmountFromOriginal(originalTotal, vat);
        }
    }
}
