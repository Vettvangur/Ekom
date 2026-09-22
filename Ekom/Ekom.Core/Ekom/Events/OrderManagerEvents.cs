using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Utilities;

namespace Ekom.Events;

public static class OrderManagerEvents
{
    public static event Func<object, OrderManagerActionExecutedEventArgs, CancellationToken, Task>? ActionExecutedAsync;

    public static Task OnActionExecutedAsync(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(ActionExecutedAsync, sender, args, ct);
}

public sealed class OrderManagerActionExecutedEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; init; }
    public required string ActionKey { get; init; }
    public string? UserName { get; init; }
    public required OrderManagerActionExecutionResult Result { get; init; }
}
