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

namespace Ekom.Controllers;

[Route("ekom/manager")]
[CamelCaseJson]
public class EkomManagerController : ControllerBase
{
    readonly ManagerRepository _repo;
    readonly IManagerAccessService _managerAccessService;
    readonly INodeService _nodeService;
    readonly ILogger<EkomManagerController> _logger;
    public EkomManagerController(ManagerRepository repo, IManagerAccessService managerAccessService, INodeService nodeService, ILogger<EkomManagerController> logger)
    {
        _repo = repo;
        _managerAccessService = managerAccessService;
        _nodeService = nodeService;
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
    [Route("SearchOrders")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string page, string pageSize)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
        }

        return Ok(await _repo.SearchOrdersAsync(start, end, query, store, orderStatus, paymentProvider, page, pageSize));
    }

    [HttpGet]
    [Route("MostSoldProducts")]
    [UmbracoUserAuthorize]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> GetMostSoldProducts(DateTime start, DateTime end, string store, string orderStatus)
    {
        if (!CanAccessStore(store))
        {
            return ForbidStore(store);
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
            await Order.Instance.UpdateStatusAsync(status, orderId, null, new ChangeOrderSettings
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

        OrderListData orders = await _repo.SearchOrdersAsync(start, end, "", store, orderStatus,"", page: "1", pageSize: "99999");

        IEnumerable<ChartDataPoint> chartDataPoints = orders.Orders.Where(x => x.PaidDate.HasValue).Select(x => new ChartDataPoint(x));

        List<ChartDataPoint> revenueChartDataPoints = chartDataPoints
                .GroupBy(record =>
                    DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                .Select(group =>
                    new ChartDataPoint()
                    {
                        x = group.Key.ToString("yyyy-MM-dd"),
                        y = Math.Round(group.Sum(x => x.y), 2, MidpointRounding.AwayFromZero)
                    })
                .ToList();

        List<ChartDataPoint> ordersChartDataPoints = chartDataPoints
                .GroupBy(record =>
                    DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                .Select(group =>
                    new ChartDataPoint()
                    {
                        x = group.Key.ToString("yyyy-MM-dd"),
                        y = group.Count()
                    })
                .ToList();

        List<ChartDataPoint> avarageChartDataPoints = chartDataPoints
                .GroupBy(record =>
                    DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                .Select(group =>
                    new ChartDataPoint()
                    {
                        x = group.Key.ToString("yyyy-MM-dd"),
                        y = Math.Round(group.Average(x => x.y), 2, MidpointRounding.AwayFromZero)
                    })
                .ToList();

        string[] labels = chartDataPoints.Select(x => x).DistinctBy(x => x).Select(x => x.x).ToArray();

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

    public class ChartData
    {
        public ChartGroupData RevenueChart { get; set; } = new ChartGroupData();
        public ChartGroupData OrdersChart { get; set; } = new ChartGroupData();
        public ChartGroupData AvarageChart { get; set; } = new ChartGroupData();
    }

    public class ChartGroupData
    {
        public string[] Labels { get; set; }
        public IEnumerable<ChartDataPoint> Points { get; set; }
    }

    public class ChartDataPoint
    {
        public ChartDataPoint()
        {

        }
        public ChartDataPoint(OrderData x1)
        {
            x = x1.PaidDate.Value.ToString("yyyy-MM-dd");
            y = x1.TotalAmount;
        }


        public string x { get; set; }
        public decimal y { get; set; }
    }


}
