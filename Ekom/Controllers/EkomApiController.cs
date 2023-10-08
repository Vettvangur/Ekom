using Microsoft.AspNetCore.Mvc;
using Ekom.Repositories;
using Ekom.Models;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    /// <summary>
    /// Public api, used by property editors
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]

    [Route("ekom/api")]
    public class EkomApiController : ControllerBase
    {
        /// <summary>
        /// 
        /// </summary>
        public EkomApiController(IServiceProvider serviceProvider, Configuration config, CountriesRepository countriesRepo)
        {
            _config = config;
            _countriesRepo = countriesRepo;
        }

        readonly CountriesRepository _countriesRepo;
        readonly Configuration _config;

        /// <summary>
        /// 
        /// </summary>
        [HttpGet]
        [Route("countries")]
        public List<Country> GetCountries()
        {
            return _countriesRepo.GetAllCountries();
        }

        /// <summary>
        /// List of all stores
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("stores")]
        public IEnumerable<IStore> GetAllStores()
        {
            return API.Store.Instance.GetAllStores();
        }
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
