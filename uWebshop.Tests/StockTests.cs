using System;
using System.Reflection;
using Microsoft.Practices.Unity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using uWebshop.Exceptions;
using uWebshop.Models.Data;
using uWebshop.Services;
using uWebshop.App_Start;
using uWebshop.Cache;
using Umbraco.Core.Models;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Moq;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Persistence;
using uWebshop.API;
using uWebshop.Interfaces;
using System.Collections.Concurrent;
using log4net;

namespace uWebshop.Tests
{
	[TestClass]
	public class StockTests
	{
		[TestMethod]
		public void DoesNotUpdateWithoutStock()
		{
			var newGuid = Guid.NewGuid();

			var logFac = new Mock<ILogFactory>();
			logFac.Setup(x => x.GetLogger(It.IsAny<Type>())).Returns(Mock.Of<ILog>());

			var stockRepo = new Mock<IStockRepository>();

			var stockCache = new StockCache(
				Mock.Of<ILogFactory>(),
				stockRepo.Object
			);
			stockCache.Cache[newGuid] = new StockData();

			var stockApi = new Stock(
				logFac.Object,
				stockCache,
				Mock.Of<IPerStoreCache<StockData>>(),
				Mock.Of<Configuration>(),
				stockRepo.Object,
				Mock.Of<IStoreService>()
			);

			Assert.ThrowsException<StockException>(() => stockApi.UpdateStock(newGuid, -5));
		}
	}
}
