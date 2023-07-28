using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers
{
    [Route("ekom/manager")]
    public class EkomManagerController : ControllerBase
    {
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
        [Route("MostSoldProducts")]
        [UmbracoUserAuthorize]
        [ResponseCache(Duration = 60 * 60 * 24)]
        public async Task<List<MostSoldProduct>> GetMostSoldProducts()
        {
            return await _repo.MostSoldProducts();
        }

        [HttpGet]
        [Route("StatusList")]
        [UmbracoUserAuthorize]
        public object GetStatusList()
        {
            return _repo.GetStatusList();
        }

        [HttpGet]
        [Route("stores")]
        [UmbracoUserAuthorize]
        public IEnumerable<IStore> GetStores()
        {
            return API.Store.Instance.GetAllStores();
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
                        new ChartDataPoint()
                        {
                            x = group.Key.ToString("yyyy-MM-dd"),
                            y = Math.Round(group.Sum(x => x.y), 2, MidpointRounding.AwayFromZero)
                        })
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

            var avarageChartDataPoints = chartDataPoints
                    .GroupBy(record =>
                        DateTime.ParseExact(record.x, "yyyy-MM-dd", null).Date)
                    .Select(group =>
                        new ChartDataPoint()
                        {
                            x = group.Key.ToString("yyyy-MM-dd"),
                            y = Math.Round(group.Average(x => x.y), 2, MidpointRounding.AwayFromZero) 
                        })
                    .ToList();

            var labels = chartDataPoints.Select(x => x).DistinctBy(x => x).Select(x => x.x).ToArray();

            chartData.RevenueChart.Points = revenueChartDataPoints;
            chartData.RevenueChart.Labels = labels;
            
            chartData.OrdersChart.Points = ordersChartDataPoints;
            chartData.OrdersChart.Labels = labels;

            chartData.AvarageChart.Points = avarageChartDataPoints;
            chartData.AvarageChart.Labels = labels;

            return chartData;
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
}
