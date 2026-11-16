namespace Ekom.Payments;

public interface IPaymentExecutionContext
{
    IDisposable EnsureContext();
}
