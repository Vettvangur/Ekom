using Ekom.Domain.Repositories;
using Ekom.Models;
using System.Collections.Generic;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Public api, used by property editors
    /// </summary>
    [PluginController("Ekom")]
    public class ApiController : UmbracoApiController
    {
        ICountriesRepository _countriesRepo;
        /// <summary>
        /// ctor
        /// </summary>
        public ApiController()
        {
            _countriesRepo = Ekom.Configuration.container.GetInstance<ICountriesRepository>();
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
