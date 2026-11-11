using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Authorization;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.Text;

namespace Ekom.Controllers;

[Route("ekom/manager")]
[CamelCaseJson]
public class EkomManagerController : ControllerBase
{
    readonly ManagerRepository _repo;
    readonly IManagerAccessService _managerAccessService;
    readonly INodeService _nodeService;
    readonly IOrderActivityLogService _orderActivityLogService;
    readonly IOrderManagerActionService _orderManagerActionService;
    readonly Providers _providers;
    readonly ILogger<EkomManagerController> _logger;
    public EkomManagerController(ManagerRepository repo, IManagerAccessService managerAccessService, INodeService nodeService, IOrderActivityLogService orderActivityLogService, IOrderManagerActionService orderManagerActionService, Providers providers, ILogger<EkomManagerController> logger)
    {
        _repo = repo;
        _managerAccessService = managerAccessService;
        _nodeService = nodeService;
        _orderActivityLogService = orderActivityLogService;
        _orderManagerActionService = orderManagerActionService;
        _providers = providers;
        _logger = logger;
    }

    [HttpPost("orderStates/{orderId:guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ChangeOrderStatesAsync(Guid orderId, [FromBody] OrderStateChangeRequest request,
        [FromServices] IOrderStateService service, CancellationToken ct = default)
    {
        try { return Ok(await service.ChangeAsync(orderId, request, ct).ConfigureAwait(false)); }
        catch (CheckoutConflictException) { return Conflict(new { message = "Checkout is locked. Reload and review payment activity before changing this order." }); }
        catch (EkomHttpException ex) { return StatusCode((int)ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpGet("statusMigration")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> PreviewStatusMigrationAsync([FromServices] IOrderStateService service, int afterReferenceId = 0, CancellationToken ct = default)
    {
        try { return Ok(await service.PreviewAsync(afterReferenceId, ct).ConfigureAwait(false)); }
        catch (EkomHttpException ex) { return StatusCode((int)ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpPost("statusMigration")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> MigrateStatusesAsync([FromServices] IOrderStateService service, int afterReferenceId = 0, CancellationToken ct = default)
    {
        try { return Ok(await service.MigrateAsync(afterReferenceId, ct).ConfigureAwait(false)); }
        catch (EkomHttpException ex) { return StatusCode((int)ex.StatusCode, new { message = ex.Message }); }
    }

    [HttpGet]
    [Route("AllOrders")]
    [UmbracoUserAuthorize]
    public async Task<IEnumerable<OrderData>> GetOrdersAsync()
    {
        return await _repo.GetOrdersAsync(_managerAccessService.GetAllowedStoreAliases());
    }

    [HttpGet]
    [Route("Order/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _repo.GetOrderAsync(orderId, ct);

        if (!CanAccessStore(order.StoreAlias))
        {
            return ForbidStore(order.StoreAlias);
        }

        return Ok(order);
    }

    [HttpGet]
    [Route("OrderInfo/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderInfoAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);
            return Ok(GetOrderInfoResponse(order));
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to get orderinfo. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }

    }

    internal static object? GetOrderInfoResponse(IOrderInfo? order)
    {
        if (order == null)
        {
            return null;
        }

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        var storeCulture = order.StoreInfo?.Culture;
        try
        {
            if (!string.IsNullOrWhiteSpace(storeCulture))
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(storeCulture);
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                }
                catch (CultureNotFoundException)
                {
                    // Older orders may contain a culture no longer supported by the host.
                }
            }

            // Materialize now: product URL getters must run under the order's store culture,
            // not later in the output formatter under the backoffice user's culture.
            var response = JObject.FromObject(order, JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

            ApplyProviderTitleFallbacks(response, order);
            return response;
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    internal static void ApplyProviderTitleFallbacks(JObject response, IOrderInfo order)
    {
        var payment = order.PaymentProvider;
        if (payment != null && string.IsNullOrWhiteSpace(payment.Title) && response["paymentProvider"] is JObject paymentResponse)
        {
            paymentResponse["title"] = OrderProviderTitleResolver.Resolve(payment.Properties, order.StoreInfo.Alias, order.Culture);
        }

        var shipping = order.ShippingProvider;
        if (shipping != null && string.IsNullOrWhiteSpace(shipping.Title) && response["shippingProvider"] is JObject shippingResponse)
        {
            shippingResponse["title"] = OrderProviderTitleResolver.Resolve(shipping.Properties, order.StoreInfo.Alias, order.Culture);
        }
    }

    [HttpGet]
    [Route("OrderLogs/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderLogsAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            return Ok(await _orderActivityLogService.GetOrderLogsAsync(orderId, ct).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order logs. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpGet]
    [Route("orderLockStatus/{orderId:guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetOrderLockStatusAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct).ConfigureAwait(false);
            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            return Ok(await _repo.GetOrderLockStatusAsync(orderId, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get checkout lock status. {OrderId}", orderId);
            return StatusCode(500, "Checkout lock status could not be loaded. Refresh before attempting recovery.");
        }
    }

    [HttpPost]
    [Route("releaseOrderLock/{orderId:guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ReleaseOrderLockAsync(Guid orderId, [FromBody] OrderLockReleaseRequest? request,
        [FromServices] IOrderManagerLockService lockService, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReleaseToken))
            return BadRequest(new { message = "Reopen the order and confirm Release Lock before trying again." });
        try
        {
            var order = await _repo.GetOrderAsync(orderId, ct).ConfigureAwait(false);
            if (!CanAccessStore(order.StoreAlias)) return ForbidStore(order.StoreAlias);
            return Ok(await lockService.ReleaseOrderLockAsync(orderId, request.ReleaseToken,
                _managerAccessService.GetAllowedStoreAliases(), HttpContext?.User?.Identity?.Name, ct).ConfigureAwait(false));
        }
        catch (EkomHttpException ex)
        {
            return StatusCode((int)ex.StatusCode, new { message = ex.Message });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Checkout lock recovery failed. OrderId: {OrderId}; FailureType: {FailureType}", orderId, ex.GetType().Name);
            return StatusCode(500, new { message = "We could not confirm that checkout was unlocked. Check the updated order before trying again, or contact support." });
        }
    }

    [HttpGet]
    [Route("giftcardRecovery/{orderId:guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetGiftcardRecoveryAsync(Guid orderId,
        [FromServices] IOrderManagerGiftcardRecoveryService recoveryService, CancellationToken ct = default)
    {
        try
        {
            return Ok(await recoveryService.GetGiftcardRecoveryAsync(orderId,
                _managerAccessService.GetAllowedStoreAliases(), ct).ConfigureAwait(false));
        }
        catch (EkomHttpException ex)
        {
            return StatusCode((int)ex.StatusCode, new { message = ex.Message });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Gift-card recovery evidence could not be loaded. OrderId: {OrderId}; FailureType: {FailureType}", orderId, ex.GetType().Name);
            return StatusCode(500, new { message = "Gift-card recovery evidence could not be loaded. Refresh or contact support." });
        }
    }

    [HttpPost]
    [Route("giftcardRecovery/{orderId:guid}/release")]
    [UmbracoUserAuthorize]
    public Task<IActionResult> ReleaseGiftcardRecoveryAsync(Guid orderId, [FromBody] GiftcardRecoveryRequest? request,
        [FromServices] IOrderManagerGiftcardRecoveryService recoveryService, CancellationToken ct = default)
        => RunGiftcardRecoveryAsync(orderId, request, false, recoveryService, ct);

    [HttpPost]
    [Route("giftcardRecovery/{orderId:guid}/reconcile")]
    [UmbracoUserAuthorize]
    public Task<IActionResult> ReconcileGiftcardRecoveryAsync(Guid orderId, [FromBody] GiftcardRecoveryRequest? request,
        [FromServices] IOrderManagerGiftcardRecoveryService recoveryService, CancellationToken ct = default)
        => RunGiftcardRecoveryAsync(orderId, request, true, recoveryService, ct);

    private async Task<IActionResult> RunGiftcardRecoveryAsync(Guid orderId, GiftcardRecoveryRequest? request,
        bool reconcile, IOrderManagerGiftcardRecoveryService recoveryService, CancellationToken ct)
    {
        if (request == null || request.AttemptId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token) ||
            !reconcile && string.IsNullOrWhiteSpace(request.ClaimId))
            return BadRequest(new { message = "Refresh the order and confirm the gift-card recovery action before trying again." });
        try
        {
            return Ok(await recoveryService.RecoverGiftcardAsync(orderId, request, reconcile,
                _managerAccessService.GetAllowedStoreAliases(), HttpContext?.User?.Identity?.Name, ct).ConfigureAwait(false));
        }
        catch (CheckoutConflictException ex)
        {
            return StatusCode(409, new { message = ex.Message });
        }
        catch (EkomHttpException ex)
        {
            return StatusCode((int)ex.StatusCode, new { message = ex.Message });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Gift-card recovery failed. OrderId: {OrderId}; AttemptId: {AttemptId}; FailureType: {FailureType}", orderId, request.AttemptId, ex.GetType().Name);
            return StatusCode(500, new { message = "Gift-card recovery could not be confirmed. Checkout remains blocked; refresh or contact support." });
        }
    }

    [HttpGet]
    [Route("OrderActions/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderActionsAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);

            if (order == null)
            {
                return NotFound();
            }

            return Ok(await _orderManagerActionService.GetActionsAsync(order, ct).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order actions. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [Route("OrderActions/{orderId}/{actionKey}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ExecuteOrderActionAsync(Guid orderId, string actionKey, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);

            if (order == null)
            {
                return NotFound();
            }

            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(orderId);
            using var activation = operation.Enter();
            OrderManagerActionExecutionResult? result = await _orderManagerActionService.ExecuteAsync(order, actionKey, HttpContext?.User?.Identity?.Name, ct).ConfigureAwait(false);

            if (result == null)
            {
                return BadRequest("Unknown order action.");
            }

            return result switch
            {
                OrderManagerActionFileResult fileResult => File(fileResult.Content, fileResult.ContentType, fileResult.FileName),
                OrderManagerActionBadRequestResult badRequestResult => BadRequest(badRequestResult.Message ?? "Order action failed."),
                _ => Ok(new OrderActionExecuteResponse { Message = result.Message })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute order action {ActionKey}. {OrderId}", actionKey, orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpGet]
    [Route("SearchOrders")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, string page, string pageSize, string? couponCode = null, string? shippingProvider = null, string? giftcardCode = null, string? currency = null, bool? needsReview = null)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        return Ok(await _repo.SearchOrdersAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, page, pageSize, couponCode, shippingProvider, giftcardCode, currency, needsReview));
    }

    [HttpGet]
    [Route("OrderViewCounts")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderViewCountsAsync(DateTime start, DateTime end, string query, string store, [FromQuery] string[] views, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, string? couponCode = null, string? shippingProvider = null, string? giftcardCode = null, string? currency = null)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        return Ok(await _repo.GetOrderViewCountsAsync(start, end, query, store, views ?? Array.Empty<string>(), paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, couponCode, shippingProvider, giftcardCode, currency));
    }

    [HttpGet]
    [Route("ExportOrders")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ExportOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, bool includeOrderLines, string? couponCode = null, string? shippingProvider = null, string? giftcardCode = null, CancellationToken ct = default, string? currency = null)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        try
        {
            List<OrderData> orders = await _repo.GetOrdersForExportAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, includeOrderLines, couponCode, shippingProvider, giftcardCode, currency).ConfigureAwait(false);
            string csv = CreateOrderCsv(orders, includeOrderLines);
            string fileName = includeOrderLines ? "orders-with-orderlines.csv" : "orders.csv";

            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export orders for store {StoreAlias}.", store);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [NonAction]
    public Task<IActionResult> ExportOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, bool includeOrderLines, string? couponCode, string? shippingProvider, CancellationToken ct)
        => ExportOrdersAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, includeOrderLines, couponCode, shippingProvider, giftcardCode: null, ct: ct);

    [NonAction]
    public Task<IActionResult> ExportOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, bool includeOrderLines, CancellationToken ct)
        => ExportOrdersAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, includeOrderLines, couponCode: null, shippingProvider: null, ct: ct);

    [HttpGet]
    [Route("MostSoldProducts")]
    [UmbracoUserAuthorize]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> GetMostSoldProducts(DateTime start, DateTime end, string store, string orderStatus, int? page = null, int? pageSize = null, string? currency = null)
    {

        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        if (page.HasValue || pageSize.HasValue)
        {
            return Ok(await _repo.MostSoldProductsPaged(start, end, store, orderStatus, page ?? 1, pageSize ?? 20, currency));
        }

        return Ok(await _repo.MostSoldProducts(start, end, store, orderStatus, currency));
    }

    [HttpGet]
    [Route("StatusList")]
    [UmbracoUserAuthorize]
    public IActionResult GetStatusList()
    {
        return Ok(_repo.GetStatusList());
    }

    [HttpGet]
    [Route("store-currencies")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetStoreCurrencies()
    {
        var result = new List<object>();
        foreach (var store in _managerAccessService.GetAllowedStores())
        {
            var recorded = await _repo.GetRecordedCurrenciesAsync(store.Alias).ConfigureAwait(false);
            var currencies = store.Currencies.Select(c => c.CurrencyValue).Concat(recorded)
                .Select(ManagerCurrencyScope.Code).Where(code => code != null).Distinct().ToArray();
            // Browsers do not ship number formats for every culture (Icelandic among them),
            // so send the format of each currency as the store's culture defines it.
            var formats = currencies.Select(code => ManagerCurrencyFormat.From(code!,
                store.Currencies.FirstOrDefault(c => string.Equals(ManagerCurrencyScope.Code(c.CurrencyValue), code, StringComparison.OrdinalIgnoreCase))?.CurrencyValue)).ToArray();
            // Dates follow the culture configured on the store, not the backoffice user's request culture.
            var dateFormat = ManagerDateFormat.From(store.Cultures.FirstOrDefault()?.Name);
            result.Add(new { store.Alias, Currencies = currencies, Formats = formats, DateFormat = dateFormat });
        }
        return Ok(result);
    }

    [HttpGet]
    [Route("stores")]
    [UmbracoUserAuthorize]
    public IActionResult GetStores()
    {
        return Ok(_managerAccessService.GetAllowedStores());
    }

    [HttpGet]
    [Route("paymentproviders/{storeAlias}")]
    [UmbracoUserAuthorize]
    public IActionResult GetPaymentProviders(string storeAlias)
    {
        if (!CanAccessStore(storeAlias))
        {
            return ForbidStore(storeAlias);
        }

        var store = _managerAccessService.GetAllowedStores()
            .FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));

        if (store == null)
        {
            return NotFound();
        }

        var culture = store.Cultures.FirstOrDefault();
        if (culture != null)
        {
            HttpContext.Features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(new RequestCulture(culture.Name), null));
        }

        return Ok(_providers.GetManagerPaymentProviders(store.Alias));
    }

    [HttpGet]
    [Route("shippingproviders/{storeAlias}")]
    [UmbracoUserAuthorize]
    public IActionResult GetShippingProviders(string storeAlias)
    {
        if (!CanAccessStore(storeAlias))
        {
            return ForbidStore(storeAlias);
        }

        var store = _managerAccessService.GetAllowedStores()
            .FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));
        if (store == null)
        {
            return NotFound();
        }

        return Ok(_providers.GetManagerShippingProviders(store.Alias)
            .Select(provider => new
            {
                provider.Key,
                Title = provider.Properties.TryGetValue("nodeName", out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name : provider.Title
            })
            .ToList());
    }

    [HttpPost]
    [Route("changeOrderStatus")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ChangeOrderStatusAsync(Guid orderId, string orderStatus, bool notify)
    {
        var order = await _repo.GetOrderAsync(orderId);

        if (!CanAccessStore(order.StoreAlias))
        {
            return ForbidStore(order.StoreAlias);
        }

        if (Enum.TryParse(orderStatus, out OrderStatus status))
        {
            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(orderId);
            using var activation = operation.Enter();
            try
            {
                await Order.Instance.UpdateStatusAsync(status, orderId, HttpContext?.User?.Identity?.Name, new ChangeOrderSettings
                {
                    FireEvents = notify

                });
            }
            catch (CheckoutConflictException ex)
            {
                return Conflict(GetStatusConflictResponse(ex.Reason, order.OrderNumber));
            }

            return Ok(true);
        }
        else
        {
            return BadRequest("Invalid order status.");
        }
    }

    internal static object GetStatusConflictResponse(CheckoutConflictReason reason, string? orderNumber)
    {
        var subject = string.IsNullOrWhiteSpace(orderNumber)
            ? "The order status"
            : $"The status of order {System.Net.WebUtility.HtmlEncode(orderNumber)}";
        var guidance = reason switch
        {
            CheckoutConflictReason.Busy => "checkout is locked. Open the order via View, confirm that no payment is processing, then use Release Lock and review its warnings before retrying the status change.",
            CheckoutConflictReason.PaymentReview => "the payment needs review. Open the order via View and check and reconcile the payment with the provider before considering Release Lock. Review its warnings; do not blindly retry the status change or payment.",
            CheckoutConflictReason.Completed => "checkout has already completed. Reload the order and verify the completed payment before making any further changes.",
            _ => "the checkout state could not be confirmed. Reload the order and review its checkout and payment state before making any further changes.",
        };

        return new
        {
            code = new CheckoutStateError(reason).Code,
            message = $"{subject} was not changed because {guidance}",
        };
    }

    [HttpPost]
    [Route("UpdateCustomerInformation")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> UpdateCustomerInformationAsync([FromBody] OrderCustomerInformationUpdateRequest? request, CancellationToken ct = default)
    {
        if (request == null || request.OrderId == Guid.Empty)
        {
            return BadRequest("Invalid customer information update request.");
        }

        try
        {
            var orderData = await _repo.GetOrderAsync(request.OrderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(request.OrderId, ct);

            if (order == null)
            {
                return NotFound();
            }

            var form = new Dictionary<string, string>
            {
                ["storeAlias"] = orderData.StoreAlias
            };

            AddFormValues(form, request.Customer);
            AddFormValues(form, request.Shipping);

            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(request.OrderId);
            using var activation = operation.Enter();
            var updatedOrder = await Order.Instance.UpdateCustomerInformationAsync(form, new OrderSettings
            {
                FireEvents = false,
                OrderInfo = order
            }, ct).ConfigureAwait(false);

            return Ok(updatedOrder);
        }
        catch (FormatException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update customer information. {OrderId}", request.OrderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [Route("Order/{orderId}/OrderLines")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> AddOrderLineAsync(Guid orderId, [FromBody] OrderLineAddRequest? request, CancellationToken ct = default)
    {
        if (request == null || request.ProductId == Guid.Empty || request.Quantity <= 0)
        {
            return BadRequest("ProductId and a positive quantity are required.");
        }

        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);

            if (order == null)
            {
                return NotFound();
            }

            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(orderId);
            using var activation = operation.Enter();
            var updatedOrder = await Order.Instance.AddOrderLineAsync(request.ProductId, request.Quantity, orderData.StoreAlias, new AddOrderSettings
            {
                FireEvents = true,
                OrderInfo = order,
                OrderAction = OrderAction.Set,
                VariantKey = request.VariantId
            }, ct).ConfigureAwait(false);

            return Ok(updatedOrder);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (EkomException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add order line. {OrderId} {ProductId}", orderId, request.ProductId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpDelete]
    [Route("Order/{orderId}/OrderLines/{lineId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> RemoveOrderLineAsync(Guid orderId, Guid lineId, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);

            if (order == null)
            {
                return NotFound();
            }

            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(orderId);
            using var activation = operation.Enter();
            var updatedOrder = await Order.Instance.RemoveOrderLineAsync(lineId, orderData.StoreAlias, new OrderSettings
            {
                FireEvents = true,
                OrderInfo = order
            }, ct).ConfigureAwait(false);

            return Ok(updatedOrder);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (EkomException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove order line. {OrderId} {OrderLineId}", orderId, lineId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    [HttpPost]
    [Route("Order/{orderId}/ShippingProvider")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> UpdateOrderShippingProviderAsync(Guid orderId, [FromBody] OrderShippingProviderUpdateRequest? request, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty || request == null || request.ProviderId == Guid.Empty)
        {
            return BadRequest("A shipping provider is required.");
        }

        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);
            if (orderData == null)
            {
                return NotFound();
            }

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            if (!_providers.GetManagerShippingProviders(orderData.StoreAlias).Any(x => x.Key == request.ProviderId))
            {
                return BadRequest("Shipping provider is not available for this store.");
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct);
            if (order == null)
            {
                return NotFound();
            }

            var customData = MergeShippingCustomData(order.ShippingProvider?.CustomData, request.CustomData);
            await using var operation = CheckoutPaymentOperationScope.BeginManagerOverride(orderId);
            using var activation = operation.Enter();
            var updatedOrder = await Order.Instance.UpdateShippingInformationAsync(
                request.ProviderId, orderData.StoreAlias, customData,
                new OrderSettings { OrderInfo = order }, ct).ConfigureAwait(false);

            if (updatedOrder.ShippingProvider?.Key != request.ProviderId)
            {
                return BadRequest("Shipping provider could not be applied to this order.");
            }

            if (customData.Any(pair => !updatedOrder.ShippingProvider.CustomData.TryGetValue(pair.Key, out var value)
                || System.Net.WebUtility.HtmlDecode(value) != pair.Value))
            {
                return BadRequest("Shipping provider custom data could not be saved.");
            }

            return Ok(GetOrderInfoResponse(updatedOrder));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (EkomException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update shipping provider for order {OrderId}", orderId);
            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    internal static Dictionary<string, string> MergeShippingCustomData(
        IReadOnlyDictionary<string, string>? existing,
        IReadOnlyDictionary<string, string>? edits)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (existing != null)
        {
            foreach (var pair in existing.Where(x => x.Key.StartsWith("customshipping", StringComparison.OrdinalIgnoreCase)))
            {
                result[pair.Key] = System.Net.WebUtility.HtmlDecode(pair.Value);
            }
        }

        if (edits != null)
        {
            var submittedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in edits)
            {
                if (!pair.Key.StartsWith("customshipping", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(pair.Key["customshipping".Length..])
                    || pair.Key.Length > 100 || pair.Value == null || pair.Value.Length > 4096)
                {
                    throw new ArgumentException("Invalid shipping custom data field.", nameof(edits));
                }

                if (!submittedKeys.Add(pair.Key))
                {
                    throw new ArgumentException("Duplicate shipping custom data field.", nameof(edits));
                }

                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    [HttpGet]
    [Route("charts")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetChartsData(DateTime start, DateTime end, string store, string orderStatus, [FromServices] CustomerRepository customers, CancellationToken ct = default, string? currency = null)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        if (start == default || end < start || end.Date >= DateTime.MaxValue.Date || (end.Date - start.Date).TotalDays > 3660)
        {
            return BadRequest("Select a valid date range of up to ten years.");
        }

        var aggregates = await _repo.GetChartAggregatesAsync(start, end, store, orderStatus, ct, currency).ConfigureAwait(false);
        var chart = BuildChartData(start, end, orderStatus, aggregates);
        if (!string.IsNullOrWhiteSpace(currency)) { chart.Currency = ManagerCurrencyScope.Code(currency); chart.MixedCurrencies = false; }
        chart.CustomerInsights = await customers.GetAnalyticsAsync(store, start, end, orderStatus, ct, currency).ConfigureAwait(false);
        return Ok(chart);
    }

    internal static ChartData BuildChartData(DateTime start, DateTime end, string orderStatus, IReadOnlyList<ChartAggregateRow> aggregates)
    {
        var byDate = aggregates.ToDictionary(row => row.BucketDate.Date);
        var amounts = new List<ChartDataPoint>();
        var orders = new List<ChartDataPoint>();
        var averages = new List<ChartDataPoint>();
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            byDate.TryGetValue(date, out var row);
            var label = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            amounts.Add(new ChartDataPoint { Date = date, x = label, y = row?.Revenue ?? 0 });
            orders.Add(new ChartDataPoint { Date = date, x = label, y = row?.Orders ?? 0 });
            averages.Add(new ChartDataPoint { Date = date, x = label, y = row?.Orders > 0 ? row.Revenue / row.Orders : null });
        }

        string[] currencies = aggregates.Select(row => row.Currency ?? "").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var labels = amounts.Select(point => point.x).ToArray();
        return new ChartData
        {
            RevenueChart = new ChartGroupData { Labels = labels, Points = amounts },
            OrdersChart = new ChartGroupData { Labels = labels, Points = orders },
            AvarageChart = new ChartGroupData { Labels = labels, Points = averages },
            DateBasis = orderStatus is "ReadyForDispatch" or "Dispatched" ? "PaidDate" : "CreateDate",
            Currency = currencies.Length == 1 ? currencies[0] : null,
            MixedCurrencies = currencies.Length > 1 || aggregates.Any(row => row.CurrencyCount > 1),
        };
    }

    private bool CanAccessStore(string? storeAlias)
    {
        return _managerAccessService.CanAccessStore(storeAlias);
    }

    private IActionResult ForbidStore(string? storeAlias)
    {
        _logger.LogWarning("Manager access denied for store {StoreAlias}", storeAlias);
        return Forbid();
    }

    private static void AddFormValues(Dictionary<string, string> form, Dictionary<string, string?> values)
    {
        foreach (var value in values)
        {
            form[value.Key] = value.Value ?? string.Empty;
        }
    }

    private static string CreateOrderCsv(IReadOnlyCollection<OrderData> orders, bool includeOrderLines)
    {
        string[] headers = includeOrderLines
            ? OrderExportHeaders.LineExportHeaders
            : OrderExportHeaders.OrderHeaders;

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', headers));

        foreach (OrderData order in orders)
        {
            AppendCsvRow(csv, headers, CreateOrderExportRow(order));

            if (!includeOrderLines || string.IsNullOrWhiteSpace(order.OrderInfo))
            {
                continue;
            }

            var orderInfo = new OrderInfo(order);

            foreach (IOrderLine orderLine in orderInfo.OrderLines)
            {
                AppendCsvRow(csv, headers, CreateOrderLineExportRow(order, orderLine));
            }
        }

        return csv.ToString();
    }

    private static Dictionary<string, string?> CreateOrderExportRow(OrderData order)
    {
        return new Dictionary<string, string?>
        {
            ["rowType"] = "Order",
            ["uniqueId"] = order.UniqueId.ToString(),
            ["referenceId"] = order.ReferenceId.ToString(CultureInfo.InvariantCulture),
            ["orderInfo"] = string.Empty,
            ["orderNumber"] = order.OrderNumber,
            ["orderStatusCol"] = order.OrderStatusCol,
            ["orderStatus"] = order.OrderStatus.ToString(),
            ["formattedTotal"] = order.FormattedTotal,
            ["customerEmail"] = order.CustomerEmail,
            ["customerName"] = order.CustomerName,
            ["customerId"] = order.CustomerId.ToString(CultureInfo.InvariantCulture),
            ["customerUsername"] = order.CustomerUsername,
            ["shippingCountry"] = order.ShippingCountry,
            ["totalAmount"] = order.TotalAmount.ToString(CultureInfo.InvariantCulture),
            ["currency"] = order.Currency,
            ["storeAlias"] = order.StoreAlias,
            ["createDate"] = FormatDate(order.CreateDate),
            ["updateDate"] = FormatDate(order.UpdateDate),
            ["paidDate"] = FormatDate(order.PaidDate),
            ["orderLineKey"] = string.Empty,
            ["productSku"] = string.Empty,
            ["productTitle"] = string.Empty,
            ["variantSku"] = string.Empty,
            ["variantTitle"] = string.Empty,
            ["quantity"] = string.Empty,
            ["unitPrice"] = string.Empty,
            ["vat"] = string.Empty,
            ["discount"] = string.Empty,
            ["lineTotal"] = string.Empty
        };
    }

    private static Dictionary<string, string?> CreateOrderLineExportRow(OrderData order, IOrderLine orderLine)
    {
        Dictionary<string, string?> row = CreateOrderExportRow(order);

        row["rowType"] = "OrderLine";
        row["orderLineKey"] = orderLine.Key.ToString();
        row["productSku"] = orderLine.Product?.SKU;
        row["productTitle"] = orderLine.Product?.Title;
        row["variantSku"] = orderLine.Variant?.SKU;
        row["variantTitle"] = orderLine.Variant?.Title;
        row["quantity"] = orderLine.Quantity.ToString(CultureInfo.InvariantCulture);
        row["unitPrice"] = orderLine.Product?.Price?.WithVat?.CurrencyString;
        row["vat"] = orderLine.Amount?.Vat?.CurrencyString;
        row["discount"] = orderLine.Amount?.DiscountAmount?.CurrencyString;
        row["lineTotal"] = orderLine.Amount?.WithVat?.CurrencyString;

        return row;
    }

    private static void AppendCsvRow(StringBuilder csv, IReadOnlyList<string> headers, IReadOnlyDictionary<string, string?> row)
    {
        csv.AppendLine(string.Join(',', headers.Select(header => EscapeCsvValue(row.TryGetValue(header, out string? value) ? value : string.Empty))));
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny(new[] { '"', ',', '\r', '\n' }) >= 0
            ? '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'
            : value;
    }

    private static string FormatDate(DateTime date) => date.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTime? date) => date?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static class OrderExportHeaders
    {
        public static readonly string[] OrderHeaders =
        {
            "uniqueId",
            "referenceId",
            "orderInfo",
            "orderNumber",
            "orderStatusCol",
            "orderStatus",
            "formattedTotal",
            "customerEmail",
            "customerName",
            "customerId",
            "customerUsername",
            "shippingCountry",
            "totalAmount",
            "currency",
            "storeAlias",
            "createDate",
            "updateDate",
            "paidDate"
        };

        public static readonly string[] LineExportHeaders = new[] { "rowType" }
            .Concat(OrderHeaders)
            .Concat(new[]
            {
                "orderLineKey",
                "productSku",
                "productTitle",
                "variantSku",
                "variantTitle",
                "quantity",
                "unitPrice",
                "vat",
                "discount",
                "lineTotal"
            })
            .ToArray();
    }

    public class ChartData
    {
        public CustomerAnalytics? CustomerInsights { get; set; }
        public string DateBasis { get; set; } = "CreateDate";
        public string? Currency { get; set; }
        public bool MixedCurrencies { get; set; }
        public ChartGroupData RevenueChart { get; set; } = new ChartGroupData();
        public ChartGroupData OrdersChart { get; set; } = new ChartGroupData();
        public ChartGroupData AvarageChart { get; set; } = new ChartGroupData();
    }

    public class ChartGroupData
    {
        public string[] Labels { get; set; } = Array.Empty<string>();
        public IEnumerable<ChartDataPoint> Points { get; set; } = Array.Empty<ChartDataPoint>();
    }

    public class ChartDataPoint
    {
        public DateTime Date { get; set; }

        public string x { get; set; } = string.Empty;
        public decimal? y { get; set; }
    }

    public sealed class OrderActionExecuteResponse
    {
        public string? Message { get; set; }
    }


}
