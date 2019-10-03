using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.Tests
{
    [TestClass]
    public class StockTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }


        [TestMethod]
        public void DoesNotUpdateWithoutStock()
        {
            var newGuid = Guid.NewGuid();

            var stockRepo = new Mock<IStockRepository>();

            var stockCache = new StockCache(
                new Configuration(),
                Mock.Of<ILogger>(),
                stockRepo.Object
            );
            stockCache[newGuid] = new StockData();

            //c.Register<IBaseCache<StockData>, StockCache>(stockCache);

            var stockApi = new Stock(
                Mock.Of<Configuration>(),
                Mock.Of<ILogger>(),
                stockCache,
                stockRepo.Object,
                Mock.Of<IDiscountStockRepository>(),
                Mock.Of<IStoreService>(),
                Mock.Of<IPerStoreCache<StockData>>()
            );

            Assert.ThrowsException<StockException>(() => stockApi.UpdateStockAsync(newGuid, -5));
        }

        [TestMethod]
        public void CanCallUpdateStockStaticMethod()
        {
            var guid = Guid.NewGuid();

            var (fac, reg) = Helpers.RegisterAll();

            var stockRepo = new Mock<IStockRepository>();
            stockRepo.Setup(sr => sr.CreateNewStockRecordAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(new StockData
                {
                    UniqueId = guid.ToString(),
                }));

            var stockCache = new StockCache(
                new Configuration(),
                Mock.Of<ILogger>(),
                stockRepo.Object
            );
            stockCache[guid] = new StockData();

            var stockSvc = new Stock(
                new Configuration(),
                Mock.Of<ILogger>(),
                stockCache,
                stockRepo.Object,
                Mock.Of<IDiscountStockRepository>(),
                Mock.Of<IStoreService>(),
                Mock.Of<IPerStoreCache<StockData>>()
            );

            reg.Register<Stock>(f => stockSvc);
            Stock.UpdateStockHangfire(guid, 1);
        }
    }
}
