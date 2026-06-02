using Ekom.Models;

namespace Ekom.Events;

public class DiscountEvents
{
    public event EventHandler<ProductDiscountEvaluationEventArgs>? BeforeEvaluateDiscounts;
    public event Func<object?, ProductDiscountApplicableEventArgs, Task>? AfterApplicableDiscounts;

    public void RaiseBeforeEvaluateDiscounts(object sender, ProductDiscountEvaluationEventArgs e)
        => BeforeEvaluateDiscounts?.Invoke(sender, e);

    public async Task RaiseAfterApplicableDiscountsAsync(object sender, ProductDiscountApplicableEventArgs e)
    {
        if (AfterApplicableDiscounts == null)
            return;

        foreach (var handler in AfterApplicableDiscounts
            .GetInvocationList()
            .Cast<Func<object?, ProductDiscountApplicableEventArgs, Task>>())
        {
            await handler(sender, e).ConfigureAwait(false);
        }
    }

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
        public List<IProductDiscount> ApplicableDiscounts { get; }

        public ProductDiscountApplicableEventArgs(
            string path,
            string storeAlias,
            decimal price,
            string[]? categories,
            List<IProductDiscount> applicableDiscounts)
        {
            Path = path;
            StoreAlias = storeAlias;
            Price = price;
            Categories = categories;
            ApplicableDiscounts = applicableDiscounts;
        }
    }

}

