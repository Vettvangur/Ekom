[Ekom docs](../README.md) / [Examples](README.md) / Add your own actions to the Manager

# Add your own actions to the Manager

An order action is a button that your code adds to an open order in Ekom Manager. These examples add a button that sends the order confirmation email again, a button that sends the order to an ERP system again, and a log entry for every action a staff member runs.

Each action is a class that implements `IOrderManagerActionProvider`. `GetActionsAsync` says which buttons the order gets, and `ExecuteAsync` runs the one the staff member clicked.

On this page:

- [Resend the order confirmation email](#resend-the-order-confirmation-email)
- [Send the order to the ERP system again](#send-the-order-to-the-erp-system-again)
- [Log every action a staff member runs](#log-every-action-a-staff-member-runs)

## Resend the order confirmation email

This action is offered on placed orders only. It asks the staff member to confirm, sends the email through your own email service, writes an entry to the order's activity log, and tells the staff member where the email went. An order without an email address gets the button disabled, with the reason under it.

The two services the examples call stand for your own code:

```csharp
using Ekom.Models;

namespace MyShop.Manager;

// Your own services. The examples only need these members.
public interface IOrderConfirmationEmail
{
    Task SendAsync(IOrderInfo order, CancellationToken ct);
}

public interface IErpOrderClient
{
    // Returns the ERP's own order number.
    Task<string> SendOrderAsync(IOrderInfo order, CancellationToken ct);
}
```

The action:

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using Microsoft.Extensions.Logging;
using OrderState = Ekom.Models.Orders.OrderStatus;

namespace MyShop.Manager;

public sealed class ResendConfirmationAction(
    IOrderConfirmationEmail confirmationEmail,
    Order orderApi,
    ISecurityService security,
    ILogger<ResendConfirmationAction> logger) : IOrderManagerActionProvider
{
    public const string Key = "myshop.resend-confirmation";

    public Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(
        IOrderInfo orderInfo,
        CancellationToken ct = default)
    {
        // Offer the button only on placed orders. Drafts and cancelled orders get nothing.
        if (!IsPlaced(orderInfo))
        {
            return Task.FromResult<IReadOnlyCollection<OrderManagerAction>>(Array.Empty<OrderManagerAction>());
        }

        string email = orderInfo.CustomerInformation.Customer.Email;
        bool hasEmail = !string.IsNullOrWhiteSpace(email);

        IReadOnlyCollection<OrderManagerAction> actions =
        [
            new OrderManagerAction
            {
                Key = Key,
                Label = "Resend order confirmation",
                ConfirmMessage = $"Send the order confirmation to {email} again?",
                Enabled = hasEmail,
                DisabledReason = hasEmail ? null : "The order has no customer email address.",
                SortOrder = 100,
            },
        ];

        return Task.FromResult(actions);
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(
        IOrderInfo orderInfo,
        string actionKey,
        string? userName = null,
        CancellationToken ct = default)
    {
        if (actionKey != Key)
        {
            return null;
        }

        // Ekom does not check that the action was listed or enabled. Check the rules again here.
        string email = orderInfo.CustomerInformation.Customer.Email;
        if (!IsPlaced(orderInfo) || string.IsNullOrWhiteSpace(email))
        {
            return new OrderManagerActionBadRequestResult
            {
                Message = "This order cannot get a confirmation email.",
            };
        }

        try
        {
            await confirmationEmail.SendAsync(orderInfo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Resending the confirmation for order {OrderNumber} in store {StoreAlias} failed",
                orderInfo.OrderNumber, orderInfo.StoreInfo.Alias);

            return new OrderManagerActionBadRequestResult
            {
                Message = "The email could not be sent. Try again later.",
            };
        }

        await orderApi.AddActivityLogAsync(
            orderInfo.UniqueId,
            $"Order confirmation sent again to {email}.",
            StaffName(userName),
            OrderActivityLogType.Info,
            ct);

        return new OrderManagerActionSuccessResult
        {
            Message = $"The order confirmation was sent to {email}.",
        };
    }

    private static bool IsPlaced(IOrderInfo orderInfo)
        => orderInfo is OrderInfo { Status.OrderStatus: OrderState.Open or OrderState.Closed };

    // userName comes from the request's identity, which can be empty for backoffice users.
    // Ekom's own status changes ask ISecurityService first.
    private string? StaffName(string? userName)
        => security.GetAuthenticatedUserName() ?? userName;
}
```

Register the actions in a composer. This one registers both actions on this page:

```csharp
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Manager;

public sealed class ManagerActionsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddTransient<IOrderManagerActionProvider, ResendConfirmationAction>();
        builder.Services.AddTransient<IOrderManagerActionProvider, SendToErpAction>();

        // Register your own IOrderConfirmationEmail and IErpOrderClient here too.
    }
}
```

What to know:

- Ekom calls `GetActionsAsync` on every registered provider each time the order opens in the Manager, and again after an action has run. Keep it fast: do not call other systems from it.
- If `GetActionsAsync` throws, the order shows no actions at all, Ekom's own included. Ekom logs the error.
- `ExecuteAsync` is called on each provider in turn until one returns a result. Return `null` for keys that are not yours. Ekom registers its own providers first, so give your keys a prefix of your own, such as `myshop.`.
- Ekom does not check that the action was listed for the order or that it was enabled. The Manager hides and disables buttons, but a request can name any key. Check your rules again in `ExecuteAsync`.
- `ConfirmMessage` is shown in the browser's confirm dialog. `DisabledReason` is shown under a disabled button.
- The three results: `OrderManagerActionSuccessResult` shows its `Message` as a success, `OrderManagerActionBadRequestResult` shows it as an error, and `OrderManagerActionFileResult` sends a file to download. [Add your own order actions](../manager/orders.md#add-your-own-order-actions) shows a file result.
- When `ExecuteAsync` throws, Ekom logs the exception and the staff member sees "An unexpected error occurred." Catch the failures you expect and return a bad-request result with a message the staff member can act on.
- After an action, the Manager loads the order, its activity log and its actions again, so a changed label or a new log entry shows at once.
- The `userName` argument comes from the request's identity, which can be empty for a backoffice user. Ekom's own status changes ask `ISecurityService.GetAuthenticatedUserName()` first, and so does this example. An activity log entry without a user name is recorded as "Customer".
- `AddActivityLogAsync` queues the entry. Ekom writes it to the database shortly after.
- `orderInfo` is the saved order, read for this request. `OrderInfo.Status` holds the order, payment and fulfillment statuses. `IOrderInfo` does not have it, so the example checks the type.

## Send the order to the ERP system again

This action lets staff send an order to the ERP by hand, for example after the ERP was down when the order came in. The label and the confirmation change when the order already has an ERP order number, and only one user group may use it.

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using Microsoft.Extensions.Logging;
using OrderState = Ekom.Models.Orders.OrderStatus;

namespace MyShop.Manager;

public sealed class SendToErpAction(
    IErpOrderClient erp,
    Order orderApi,
    ISecurityService security,
    ILogger<SendToErpAction> logger) : IOrderManagerActionProvider
{
    public const string Key = "myshop.send-to-erp";

    // Your automatic ERP export saves the ERP's order number in the order's custom data under this key.
    public const string ErpOrderNumberKey = "erpOrderNumber";

    // The user group alias that may send orders to the ERP by hand.
    private const string ErpGroup = "erpOperators";

    public Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(
        IOrderInfo orderInfo,
        CancellationToken ct = default)
    {
        if (!IsPlaced(orderInfo))
        {
            return Task.FromResult<IReadOnlyCollection<OrderManagerAction>>(Array.Empty<OrderManagerAction>());
        }

        string? erpNumber = orderInfo.CustomData.GetValueOrDefault(ErpOrderNumberKey);
        bool alreadySent = !string.IsNullOrWhiteSpace(erpNumber);
        bool allowed = MayUse();

        IReadOnlyCollection<OrderManagerAction> actions =
        [
            new OrderManagerAction
            {
                Key = Key,
                Label = alreadySent ? "Send to ERP again" : "Send to ERP",
                // A filled button while the order is missing from the ERP, an outline button after.
                Look = alreadySent ? "outline" : "primary",
                ConfirmMessage = alreadySent
                    ? $"The ERP already has this order as {erpNumber}. Send it again?"
                    : "Send this order to the ERP now?",
                Enabled = allowed,
                DisabledReason = allowed ? null : "Only the ERP operators group can send orders to the ERP.",
                SortOrder = 110,
            },
        ];

        return Task.FromResult(actions);
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(
        IOrderInfo orderInfo,
        string actionKey,
        string? userName = null,
        CancellationToken ct = default)
    {
        if (actionKey != Key)
        {
            return null;
        }

        if (!IsPlaced(orderInfo) || !MayUse())
        {
            return new OrderManagerActionBadRequestResult
            {
                Message = "You cannot send this order to the ERP.",
            };
        }

        string erpNumber;
        try
        {
            erpNumber = await erp.SendOrderAsync(orderInfo, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Sending order {OrderNumber} in store {StoreAlias} to the ERP failed",
                orderInfo.OrderNumber, orderInfo.StoreInfo.Alias);

            return new OrderManagerActionBadRequestResult
            {
                Message = "The ERP did not accept the order. Try again later.",
            };
        }

        await orderApi.AddActivityLogAsync(
            orderInfo.UniqueId,
            $"Sent to the ERP by hand. ERP order number: {erpNumber}.",
            security.GetAuthenticatedUserName() ?? userName,
            OrderActivityLogType.Success,
            ct);

        return new OrderManagerActionSuccessResult
        {
            Message = $"The order is in the ERP as {erpNumber}.",
        };
    }

    private static bool IsPlaced(IOrderInfo orderInfo)
        => orderInfo is OrderInfo { Status.OrderStatus: OrderState.Open or OrderState.Closed };

    // Ekom only checks that the user may open the order's store. Limit the action further yourself.
    private bool MayUse()
        => security.IsCurrentUserAdmin()
            || security.GetUmbracoUserGroups().Contains(ErpGroup, StringComparer.OrdinalIgnoreCase);
}
```

The composer in the first example registers it.

Who can run an action:

1. The user must be signed in to the Umbraco backoffice and allowed to use the Manager. Administrators always are. Other users must belong to a group in `Ekom:Manager:SectionAccessGroup` or a group listed for any store in `Ekom:Manager:StoreGroupPermissions`. When neither setting names a group, every signed-in backoffice user is allowed. Otherwise the request gets `403`.
2. The user must be allowed to work with the order's store. Ekom checks this before it lists the actions and again before it runs one, and answers `403` otherwise. With an empty `StoreGroupPermissions`, every store is allowed. Otherwise only administrators and the groups listed for that store are. See [Manager access and permissions](../manager/access-and-permissions.md).
3. Anything stricter is yours to check. Ekom has no permission per action. This example injects `ISecurityService` and checks the user's group aliases, both when it lists the action and when it runs it.

What to know:

- `orderInfo.CustomData` holds the order's custom data. The example reads the ERP order number that your automatic ERP export saved under `erpOrderNumber`. See [Order custom data](../guides/order-custom-data.md).
- `Look = "primary"` draws a filled button. Any other value draws an outline button.
- Make the ERP call safe to repeat. Staff can click the button twice, and a request that timed out may still have reached the ERP.
- The example catches `HttpRequestException` only. Any other exception reaches Ekom, which logs it and shows a general error.
- Ekom runs the action with a Manager override for the order. Order changes your action makes are allowed while a payment attempt is idle, and refused while a checkout operation is running for the order.

## Log every action a staff member runs

After an action returns a success result or a file, Ekom raises `OrderManagerEvents.ActionExecutedAsync`. This component writes each of your actions to the application log with the user who ran it.

```csharp
using Ekom.Events;
using Ekom.Models.Manager;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Manager;

public sealed class OrderActionAuditComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<OrderActionAuditComponent>();
}

public sealed class OrderActionAuditComponent(ILogger<OrderActionAuditComponent> logger) : IAsyncComponent
{
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderManagerEvents.ActionExecutedAsync += OnActionExecutedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderManagerEvents.ActionExecutedAsync -= OnActionExecutedAsync;
        return Task.CompletedTask;
    }

    private Task OnActionExecutedAsync(object sender, OrderManagerActionExecutedEventArgs args, CancellationToken ct)
    {
        // Runs for every provider's actions, Ekom's own included. Keep to yours.
        if (!args.ActionKey.StartsWith("myshop.", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        string outcome = args.Result is OrderManagerActionFileResult ? "file" : args.Result.Message ?? string.Empty;

        // Do not throw here: the action has already run, and the user would see an error.
        logger.LogInformation(
            "Manager action {ActionKey} ran on order {OrderNumber} in store {StoreAlias}. User: {UserName}. Result: {Outcome}",
            args.ActionKey,
            args.OrderInfo.OrderNumber,
            args.OrderInfo.StoreInfo.Alias,
            args.UserName,
            outcome);

        return Task.CompletedTask;
    }
}
```

What to know:

- `ActionExecutedAsync` is the only event for Manager actions. It runs after the action, and is not raised for a bad-request result. There is no event before an action; put your checks in `ExecuteAsync`.
- The event runs for every action, Ekom's own included, so filter by key.
- If a handler throws, the staff member sees an error even though the action has already run. Do not throw from a handler, and make any work in it safe to repeat.
- `args.UserName` is the same `userName` that `ExecuteAsync` received, and can be empty.
- `OrderManagerEvents` is a static class. Subscribe once at startup and unsubscribe at shutdown, as the component does. See [Subscribe to an event](../reference/events.md#subscribe-to-an-event).

## Related pages

- [Manager orders](../manager/orders.md#add-your-own-order-actions): how actions look to staff, and the result types.
- [Manager access and permissions](../manager/access-and-permissions.md): who may use the Manager and each store.
- [Activity logs](../manager/activity-logs.md): where the log entries from these actions appear.
- [Events](../reference/events.md#manager-action-events): `OrderManagerEvents` and the other events.
- [Services and dependency injection](../reference/services-and-dependency-injection.md#add-actions-to-the-manager-order-view): registering your own services.

[Back to Examples](README.md)
