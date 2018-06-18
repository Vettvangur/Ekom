﻿using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Public api, used by property editors
    /// </summary>
    [PluginController("Ekom")]
    public class ApiController : UmbracoAuthorizedApiController
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

        /// <summary>
        /// List of all stores
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IStore> GetAllStores()
        {
            return API.Store.Instance.GetAllStores();
        }

        /// <summary>
        /// Get Config
        /// </summary>
        public object GetConfig()
        {
            return Ekom.Configuration.Current;
        }

        /// <summary>
        /// Get Stock By Store
        /// </summary>
        /// <returns></returns>
        public int GetStockByStore(Guid id, string storeAlias)
        {
            return API.Stock.Instance.GetStock(id, storeAlias);
        }

        /// <summary>
        /// Get Stock 
        /// </summary>
        /// <returns></returns>
        public int GetStock(Guid id)
        {
            return API.Stock.Instance.GetStock(id);
        }

        /// <summary>
        /// Update Stock
        /// </summary>
        [HttpPost]
        public HttpResponseMessage UpdateStock(Guid id, int stock)
        {
            API.Stock.Instance.UpdateStock(id, stock);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Update Stock
        /// </summary>
        [HttpPost]
        public HttpResponseMessage UpdateStock(Guid id, string storeAlias, int stock)
        {
            API.Stock.Instance.UpdateStock(id, stock);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Set Stock
        /// </summary>
        [HttpPost]
        public HttpResponseMessage SetStock(Guid id, int stock)
        {
            API.Stock.Instance.SetStock(id, stock);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Set Stock
        /// </summary>
        [HttpPost]
        public HttpResponseMessage SetStock(Guid id, string storeAlias, int stock)
        {
            API.Stock.Instance.SetStock(id, storeAlias, stock);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
