namespace uWebshop.Interfaces
{
    public interface IPriceCalculationService
    {
        int WithVat(int originalTotal, decimal vat);
        int WithoutVat(int originalTotal, decimal vat);
        int Vat(int originalTotal, decimal vat);
    }
}
