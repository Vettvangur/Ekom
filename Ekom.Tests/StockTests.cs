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

namespace Ekom.Tests
{
    [TestClass]
    public class StockTests
    {
        [TestMethod]
        public void DoesNotUpdateWithoutStock()
        {
            var newGuid = Guid.NewGuid();

            var c = TinyIoCActivator.Start();

            var logFac = new Mock<ILogFactory>();
            logFac.Setup(x => x.GetLogger(It.IsAny<Type>())).Returns(Mock.Of<ILog>());

            var stockRepo = new Mock<IStockRepository>();

            var stockCache = new StockCache(
                Mock.Of<ILogFactory>(),
                stockRepo.Object
            );
            stockCache[newGuid] = new StockData();

            c.Register<IBaseCache<StockData>, StockCache>(stockCache);

            var stockApi = new Stock(
                Mock.Of<Configuration>(),
                logFac.Object,
                stockCache,
                stockRepo.Object,
                Mock.Of<IDiscountStockRepository>(),
                Mock.Of<IStoreService>(),
                Mock.Of<IPerStoreCache<StockData>>()
            );

            Assert.ThrowsException<StockException>(() => stockApi.UpdateStock(newGuid, -5));
        }

        [TestMethod]
        public void CanCallUpdateStockStaticMethod()
        {
            var guid = Guid.NewGuid();

            var mockedContainer = Helpers.InitMockContainer();

            var stockRepo = new Mock<IStockRepository>();
            stockRepo.Setup(sr => sr.CreateNewStockRecord(It.IsAny<string>()))
                .Returns(new StockData
                {
                    UniqueId = guid.ToString(),
                });

            var stockCache = new StockCache(
                Mock.Of<ILogFactory>(),
                stockRepo.Object
            );
            stockCache[guid] = new StockData();

            var stockSvc = new Stock(
                new Configuration(),
                Helpers.MockLogFac(),
                stockCache,
                stockRepo.Object,
                Mock.Of<IDiscountStockRepository>(),
                Mock.Of<IStoreService>(),
                Mock.Of<IPerStoreCache<StockData>>()
            );

            mockedContainer.Setup(c => c.GetInstance<Stock>()).Returns(stockSvc);
            Stock.UpdateStockHangfire(guid, 1);
        }
    }
}
