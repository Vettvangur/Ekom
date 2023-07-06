using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using static LinqToDB.Common.Configuration;

namespace Ekom.Controllers
{

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]

    [Route("ekom/manager")]
    public class EkomManagerController : ControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        public EkomManagerController(ManagerRepository repo)
        {
            _repo = repo;
        }
        readonly ManagerRepository _repo;

        [HttpGet]
        [Route("AllOrders")]
        [UmbracoUserAuthorize]
        public async Task<IEnumerable<OrderData>> GetOrdersAsync() {
            return await _repo.GetOrdersAsync();
        }

        [HttpGet]
        [Route("Order/{orderId}")]
        [UmbracoUserAuthorize]
        public async Task<OrderData> GetOrderAsync(Guid orderId)
        {
            return await _repo.GetOrderAsync(orderId);
        }

        [HttpGet]
        [Route("OrderInfo/{orderId}")]
        [UmbracoUserAuthorize]
        public IOrderInfo GetOrderInfo(Guid orderId)
        {
            return _repo.GetOrderInfo(orderId);
        }

        [HttpGet]
        [Route("SearchOrders")]
        [UmbracoUserAuthorize]
        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string page, string pageSize)
        {
            return await _repo.SearchOrdersAsync(start,end,query,store,orderStatus,page, pageSize);
        }

        [HttpGet]
        [Route("StatusList")]
        [UmbracoUserAuthorize]
        public object GetStatusList()
        {
            return _repo.GetStatusList();
        }

        [HttpGet]
        [Route("charts")]
        [UmbracoUserAuthorize]
        public async Task<ChartData> GetChartsData(DateTime start, DateTime end, string orderStatus)
        {
            var chartData = new ChartData();

            var orders =  await _repo.SearchOrdersAsync(start, end, "", "", orderStatus, "1", "99999");

            var chartDataPoints = orders.Orders.Where(x => x.PaidDate.HasValue).Select(x => new ChartDataPoint(x));

            var revenueChartDataPoints = chartDataPoints
                    .GroupBy(record =>
                        DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                    .Select(group =>
                        new ChartDataPoint(group))
                    .ToList();

            var ordersChartDataPoints = chartDataPoints
                    .GroupBy(record =>
                        DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                    .Select(group =>
                        new ChartDataPoint()
                        {
                            x = group.Key.ToString("yyyy-MM-dd"),
                            y = group.Count()
                        })
                    .ToList();

            var labels = chartDataPoints.Select(x => x).DistinctBy(x => x).Select(x => x.x).ToArray();

            chartData.RevenueChart.Points = revenueChartDataPoints;
            chartData.RevenueChart.Labels = labels;
            
            chartData.OrdersChart.Points = ordersChartDataPoints;
            chartData.OrdersChart.Labels = labels;

            return chartData;
        }

        public class ChartData
        {
            public ChartGroupData RevenueChart { get; set; } = new ChartGroupData();
            public ChartGroupData OrdersChart { get; set; } = new ChartGroupData();
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

            public ChartDataPoint(IGrouping<DateTime, ChartDataPoint> group)
            {
                x = group.Key.ToString("yyyy-MM-dd");
                y = group.Sum(record => record.y);
            }

            public string x { get; set; }
            public decimal y { get; set; }
        }


    }
}
