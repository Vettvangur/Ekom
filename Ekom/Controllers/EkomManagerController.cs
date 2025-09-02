using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

[Route("ekom/manager")]
[CamelCaseJson]
public class EkomManagerController : ControllerBase
{
    readonly ManagerRepository _repo;
    readonly INodeService _nodeService;
    public EkomManagerController(ManagerRepository repo, INodeService nodeService)
    {
        _repo = repo;
        _nodeService = nodeService;
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
    public async Task<IActionResult> GetOrderAsync(Guid orderId)
    {
        return Ok(await _repo.GetOrderAsync(orderId));
    }

    [HttpGet]
    [Route("OrderInfo/{orderId}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderInfoAsync(Guid orderId)
    {
        try
        {
            return Ok(await _repo.GetOrderInfoAsync(orderId));
        }
        catch(Exception ex)
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }

    }

    [HttpGet]
    [Route("SearchOrders")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string paymentProvider, string page, string pageSize)
    {
        return Ok(await _repo.SearchOrdersAsync(start, end, query, store, orderStatus, paymentProvider, page, pageSize));
    }

    [HttpGet]
    [Route("MostSoldProducts")]
    [UmbracoUserAuthorize]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> GetMostSoldProducts(DateTime start, DateTime end, string store, string orderStatus)
    {
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
