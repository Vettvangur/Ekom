using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;

namespace Ekom.Tests
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
