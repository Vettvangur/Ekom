using Ekom.Models;

namespace Ekom.Events;

public class DiscountEvents
{
    public event EventHandler<ProductDiscountEvaluationEventArgs>? BeforeEvaluateDiscounts;
    public event EventHandler<ProductDiscountApplicableEventArgs>? AfterApplicableDiscounts;

    public void RaiseBeforeEvaluateDiscounts(object sender, ProductDiscountEvaluationEventArgs e)
        => BeforeEvaluateDiscounts?.Invoke(sender, e);

    public void RaiseAfterApplicableDiscounts(object sender, ProductDiscountApplicableEventArgs e)
        => AfterApplicableDiscounts?.Invoke(sender, e);

    public class ProductDiscountEvaluationEventArgs : EventArgs
    {
        public string Path { get; set; } = string.Empty;
        public string StoreAlias { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string[]? Categories { get; set; }
    }

    public class ProductDiscountApplicableEventArgs : EventArgs
    {
        public string Path { get; }
        public string StoreAlias { get; }
        public decimal Price { get; }
        public string[]? Categories { get; }
        public IList<IProductDiscount> ApplicableDiscounts { get; }

        public ProductDiscountApplicableEventArgs(
            string path,
            string storeAlias,
            decimal price,
            string[]? categories,
            IList<IProductDiscount> applicableDiscounts)
        {
            Path = path;
            StoreAlias = storeAlias;
            Price = price;
            Categories = categories;
            ApplicableDiscounts = applicableDiscounts;
        }
    }

}


