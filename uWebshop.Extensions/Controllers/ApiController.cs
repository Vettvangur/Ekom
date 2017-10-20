using System.Collections.Generic;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using uWebshop.Domain.Repositories;
using uWebshop.Models;

namespace uWebshop.Extensions.Controllers
{
    /// <summary>
    /// Public api, used by property editors
    /// </summary>
    [PluginController("uWebshop")]
    public class ApiController : UmbracoApiController
    {
        ICountriesRepository _countriesRepo;
        /// <summary>
        /// ctor
        /// </summary>
        public ApiController()
        {
            _countriesRepo = uWebshop.Configuration.container.GetInstance<ICountriesRepository>();
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="countriesRepo"></param>
        public ApiController(ICountriesRepository countriesRepo)
        {
            _countriesRepo = countriesRepo;
        }

        /// <summary>
        /// List of countries with name and code.
        /// </summary>
        /// <returns></returns>
        public List<Country> GetCountries()
        {
            return _countriesRepo.GetAllCountries();
        }
    }
}
