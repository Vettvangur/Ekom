using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class CouponRedemptionTests
{
    [Fact]
    public async Task SingleUseCouponCanOnlyBeConsumedOnce()
    {
        using var fixture = new Fixture();
        fixture.AddCoupon("once", numberAvailable: 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Repository.MarkUsedAsync("once")));

        Assert.Equal(1, results.Count(consumed => consumed));
        Assert.Equal(0, fixture.NumberAvailable("once"));
    }

    [Fact]
    public async Task UnknownOrExhaustedCouponIsReportedAndNeverGoesNegative()
    {
        using var fixture = new Fixture();
        fixture.AddCoupon("spent", numberAvailable: 0);

        Assert.False(await fixture.Repository.MarkUsedAsync("spent"));
        Assert.False(await fixture.Repository.MarkUsedAsync("missing"));
        Assert.Equal(0, fixture.NumberAvailable("spent"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-coupons-{Guid.NewGuid():N}.sqlite");

        public DatabaseFactory Factory { get; }
        public CouponRepository Repository { get; }

        public Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            Factory = new DatabaseFactory(config, host.Object);

            var ekomConfig = new Mock<Configuration>(config);
            ekomConfig.SetupGet(x => x.CacheList).Returns(new Lazy<List<Cache.ICache>>(() => []));

            Repository = new CouponRepository(
                ekomConfig.Object,
                NullLogger<CouponRepository>.Instance,
                Mock.Of<ICouponCache>(),
                Factory,
                Mock.Of<INodeService>());

            using var db = Factory.GetDatabase();
            db.CreateTable<CouponData>();
        }

        public void AddCoupon(string code, int numberAvailable)
        {
            using var db = Factory.GetDatabase();
            db.Insert(new CouponData
            {
                CouponCode = code,
                NumberAvailable = numberAvailable,
                CouponKey = Guid.NewGuid(),
                DiscountId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
            });
        }

        public int NumberAvailable(string code)
        {
            using var db = Factory.GetDatabase();
            return db.GetTable<CouponData>().Where(x => x.CouponCode == code).Select(x => x.NumberAvailable).Single();
        }

        public void Dispose() => File.Delete(_path);
    }
}
