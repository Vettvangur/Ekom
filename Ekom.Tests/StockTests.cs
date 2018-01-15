using Ekom.API;
using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Unity;

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
            stockCache[newGuid] = new StockData();

            var stockApi = new Stock(
                logFac.Object,
                stockCache,
                Mock.Of<Configuration>(),
                stockRepo.Object,
                Mock.Of<IDiscountStockRepository>()
            );

            Assert.ThrowsException<StockException>(() => stockApi.UpdateStock(newGuid, -5));
        }

        [TestMethod]
        public void CanCallUpdateStockStaticMethod()
        {
            var guid = Guid.NewGuid();

            var c = UnityConfig.GetConfiguredContainer();
            var stockRepo = new Mock<IStockRepository>();
            stockRepo.Setup(sr => sr.CreateNewStockRecord(It.IsAny<string>()))
                .Returns(new StockData
                {
                    UniqueId = guid.ToString(),
                });

            c.RegisterInstance(stockRepo.Object);

            Stock.UpdateStockHangfire(guid, 1);
        }
    }
}
