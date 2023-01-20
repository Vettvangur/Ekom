#if NETFRAMEWORK
using System.Web.Http;
#else
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
#endif

using Ekom.Domain.Repositories;
using Ekom.Models;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System;

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
#if NETFRAMEWORK
    public class EkomApiController : ApiController
    {
        /// <summary>
        /// 
        /// </summary>
        public EkomApiController()
        {
            _config = Ekom.Configuration.Resolver.GetService<Configuration>();
            _countriesRepo = Ekom.Configuration.Resolver.GetService<CountriesRepository>();
        }
#else
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

#endif

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
