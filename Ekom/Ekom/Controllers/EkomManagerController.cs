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
    readonly INodeService _nodeService;
    readonly ILogger<EkomManagerController> _logger;
    public EkomManagerController(ManagerRepository repo, INodeService nodeService, ILogger<EkomManagerController> logger)
    {
        _repo = repo;
        _nodeService = nodeService;
        _logger = logger;
    }

    [HttpGet]
    [Route("AllOrders")]
    [UmbracoUserAuthorize]
    public async Task<IEnumerable<OrderData>> GetOrdersAsync()
    {
        return await _repo.GetOrdersAsync();
    }

    [HttpGet]
    [Route("Order/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        return Ok(await _repo.GetOrderAsync(orderId, ct));
    }

    [HttpGet]
    [Route("OrderInfo/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderInfoAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
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
    public async Task<IActionResult> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string productSku, string page, string pageSize)
    {
        return Ok(await _repo.SearchOrdersAsync(start, end, query, store, orderStatus, paymentProvider, productSku, page, pageSize));
    }

    [HttpGet]
    [Route("MostSoldProducts")]
    [UmbracoUserAuthorize]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> GetMostSoldProducts(DateTime start, DateTime end, string store, string orderStatus, int? page = null, int? pageSize = null)
    {
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
        return Ok(API.Store.Instance.GetAllStores());
    }

    [HttpPost]
    [Route("changeOrderStatus")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> ChangeOrderStatusAsync(Guid orderId, string orderStatus, bool notify)
    {
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


}
