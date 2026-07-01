using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    readonly ILogger<EkomManagerController> _logger;
    public EkomManagerController(ManagerRepository repo, IManagerAccessService managerAccessService, INodeService nodeService, IOrderActivityLogService orderActivityLogService, IOrderManagerActionService orderManagerActionService, ILogger<EkomManagerController> logger)
    {
        _repo = repo;
        _managerAccessService = managerAccessService;
        _nodeService = nodeService;
        _orderActivityLogService = orderActivityLogService;
        _orderManagerActionService = orderManagerActionService;
        _logger = logger;
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
            return Ok(order);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to get orderinfo. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
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
    public async Task<IActionResult> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, string page, string pageSize)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        return Ok(await _repo.SearchOrdersAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, page, pageSize));
    }

    [HttpGet]
    [Route("ExportOrders")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ExportOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string trackingSource, string trackingMedium, string trackingCampaign, string trackingTerm, string trackingContent, string trackingClickId, bool includeOrderLines, CancellationToken ct = default)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        try
        {
            List<OrderData> orders = await _repo.GetOrdersForExportAsync(start, end, query, store, orderStatus, paymentProvider, productSku, trackingSource, trackingMedium, trackingCampaign, trackingTerm, trackingContent, trackingClickId, includeOrderLines).ConfigureAwait(false);
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

    [HttpGet]
    [Route("MostSoldProducts")]
    [UmbracoUserAuthorize]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> GetMostSoldProducts(DateTime start, DateTime end, string store, string orderStatus, int? page = null, int? pageSize = null)
    {

        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }
        
        if (page.HasValue || pageSize.HasValue)
        {
            return Ok(await _repo.MostSoldProductsPaged(start, end, store, orderStatus, page ?? 1, pageSize ?? 20));
        }

        return Ok(await _repo.MostSoldProducts(start, end, store, orderStatus));
    }

    [HttpGet]
    [Route("StatusList")]
    [UmbracoUserAuthorize]
    public IActionResult GetStatusList()
    {
        return Ok(_repo.GetStatusList());
    }

    [HttpGet]
    [Route("stores")]
    [UmbracoUserAuthorize]
    public IActionResult GetStores()
    {
        return Ok(_managerAccessService.GetAllowedStores());
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
            await Order.Instance.UpdateStatusAsync(status, orderId, HttpContext?.User?.Identity?.Name, new ChangeOrderSettings
            {
                FireEvents = notify

            });

            return Ok(true);
        }
        else
        {
            return BadRequest("Invalid order status.");
        }
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

    [HttpGet]
    [Route("charts")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetChartsData(DateTime start, DateTime end, string store, string orderStatus)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        var chartData = new ChartData();

        List<ChartAggregateRow> aggregates = await _repo.GetChartAggregatesAsync(start, end, store, orderStatus);

        string[] labels = aggregates
            .Select(x => x.BucketDate.ToString("yyyy-MM-dd"))
            .ToArray();

        List<ChartDataPoint> revenueChartDataPoints = aggregates
            .Select(x => new ChartDataPoint
            {
                Date = x.BucketDate,
                x = x.BucketDate.ToString("yyyy-MM-dd"),
                y = Math.Round(x.Revenue, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        List<ChartDataPoint> ordersChartDataPoints = aggregates
            .Select(x => new ChartDataPoint
            {
                Date = x.BucketDate,
                x = x.BucketDate.ToString("yyyy-MM-dd"),
                y = x.Orders
            })
            .ToList();

        List<ChartDataPoint> avarageChartDataPoints = aggregates
            .Select(x => new ChartDataPoint
            {
                Date = x.BucketDate,
                x = x.BucketDate.ToString("yyyy-MM-dd"),
                y = Math.Round(x.AverageAmount, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        chartData.RevenueChart.Points = revenueChartDataPoints;
        chartData.RevenueChart.Labels = labels;

        chartData.OrdersChart.Points = ordersChartDataPoints;
        chartData.OrdersChart.Labels = labels;

        chartData.AvarageChart.Points = avarageChartDataPoints;
        chartData.AvarageChart.Labels = labels;

        return Ok(chartData);
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
        public decimal y { get; set; }
    }

    public sealed class OrderActionExecuteResponse
    {
        public string? Message { get; set; }
    }


}
