using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Microsoft.AspNetCore.Mvc;

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
        [Route("Orders")]
        [UmbracoUserAuthorize]
        public async Task<IEnumerable<OrderData>> GetOrdersAsync() {
            return await _repo.GetOrdersAsync();
        }
        
        [HttpGet]
        [Route("SearchOrders")]
        [UmbracoUserAuthorize]
        public async Task<OrderListData> SearchOrdersAsync(DateTime start, DateTime end, string query, string store, string orderStatus, string page, string pageSize)
        {
            return await _repo.SearchOrdersAsync(start,end,query,store,orderStatus,page, pageSize);
        }


    }
}
