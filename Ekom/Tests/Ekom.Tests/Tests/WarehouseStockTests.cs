using Ekom.Cache;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using WarehouseApi = Ekom.API.Warehouse;

namespace Ekom.Tests.Tests;

public class WarehouseStockTests
{
    private const string DefaultStore = "default";
    private static readonly Guid MainWarehouse = Guid.Parse("d8cf6892-f075-430b-b94b-f43f5828bbca");
    private static readonly Guid SecondaryWarehouse = Guid.Parse("1365c4a6-8dca-4369-969b-2d4dff62a901");

    [Fact]
    public async Task GetAsync_BeforeInitialCacheFill_ThrowsCacheUnavailable()
    {
        var repository = new FakeWarehouseStockRepository();
        WarehouseApi warehouse = CreateWarehouse(repository, out _);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1"));

        Assert.Contains("has not been initialized", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, repository.GetAllCallCount);
    }

    [Fact]
    public async Task Reads_AfterCacheFill_DoNotReloadRepository()
    {
        var repository = new FakeWarehouseStockRepository();
        repository.Data.Add(CreateData(DefaultStore, MainWarehouse, "SKU-1", 4m));
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        WarehouseStockBalance? single = await warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1");
        IReadOnlyList<WarehouseStockBalance> warehouseBalances = await warehouse.GetAsync(
            DefaultStore,
            MainWarehouse,
            ["sku-1"]);
        IReadOnlyList<WarehouseStockBalance> storeBalances = await warehouse.GetAsync(DefaultStore, ["sku-1"]);
        IReadOnlyList<WarehouseStockLevel> levels = await warehouse.GetForSkuAsync(DefaultStore, "sku-1");

        Assert.Equal(4m, single?.Balance);
        Assert.Single(warehouseBalances);
        Assert.Single(storeBalances);
        Assert.Equal(4m, levels.Single(level => level.WarehouseKey == MainWarehouse).Balance);
        Assert.Equal(1, repository.GetAllCallCount);
    }

    [Fact]
    public async Task SetAndClear_PreserveUnsetZeroAndUnchangedUpdateDate()
    {
        var repository = new FakeWarehouseStockRepository();
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        bool clearedUnset = await warehouse.ClearAsync(DefaultStore, MainWarehouse, "sku-1");
        WarehouseStockBalance inserted = await warehouse.SetAsync(DefaultStore, MainWarehouse, "sku-1", 0m);
        WarehouseStockBalance unchanged = await warehouse.SetAsync(DefaultStore, MainWarehouse, "sku-1", 0m);
        bool clearedZero = await warehouse.ClearAsync(DefaultStore, MainWarehouse, "sku-1");
        WarehouseStockBalance? afterClear = await warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1");

        Assert.False(clearedUnset);
        Assert.Equal(0m, inserted.Balance);
        Assert.Equal(inserted.UpdateDate, unchanged.UpdateDate);
        Assert.True(clearedZero);
        Assert.Null(afterClear);
        Assert.Equal(2, repository.ApplyCallCount);
        Assert.Empty(repository.Data);
    }

    [Fact]
    public async Task UpdateAsync_ReportsValidationAndPersistenceFailuresPerEntry()
    {
        var repository = new FakeWarehouseStockRepository();
        repository.FailingSkus.Add("FAIL");
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        WarehouseStockBatchResult result = await warehouse.UpdateAsync([
            new WarehouseStockMutationRequest
            {
                StoreAlias = DefaultStore,
                WarehouseKey = MainWarehouse,
                Sku = "first",
                Balance = 1m,
            },
            new WarehouseStockMutationRequest
            {
                StoreAlias = DefaultStore,
                WarehouseKey = Guid.NewGuid(),
                Sku = "invalid-warehouse",
                Balance = 2m,
            },
            new WarehouseStockMutationRequest
            {
                StoreAlias = DefaultStore,
                WarehouseKey = MainWarehouse,
                Sku = "fail",
                Balance = 3m,
            },
            new WarehouseStockMutationRequest
            {
                StoreAlias = "second",
                WarehouseKey = SecondaryWarehouse,
                Sku = "last",
                Balance = 4m,
            },
        ]);

        Assert.Equal(2, result.Inserted);
        Assert.Equal(2, result.Failed);
        Assert.Equal(WarehouseStockMutationStatus.Inserted, result.Entries[0].Status);
        Assert.Equal(WarehouseStockMutationStatus.Failed, result.Entries[1].Status);
        Assert.Equal(WarehouseStockMutationStatus.Failed, result.Entries[2].Status);
        Assert.Equal(WarehouseStockMutationStatus.Inserted, result.Entries[3].Status);
        Assert.Equal(1, repository.ApplyCallCount);
        Assert.Equal(2, repository.Data.Count);
    }

    [Fact]
    public async Task ExistingSetBatch_RemainsFailFast()
    {
        var repository = new FakeWarehouseStockRepository();
        repository.FailingSkus.Add("FAIL");
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        await Assert.ThrowsAsync<InvalidOperationException>(() => warehouse.SetAsync(
            DefaultStore,
            MainWarehouse,
            [
                new WarehouseStockBalanceRequest { Sku = "fail", Balance = 1m },
                new WarehouseStockBalanceRequest { Sku = "not-written", Balance = 2m },
            ]));

        Assert.Equal(1, repository.ApplyCallCount);
        Assert.Empty(repository.Data);
    }

    [Fact]
    public async Task SetAsync_NormalizesStoreAliasIdentityForNewRecords()
    {
        var repository = new FakeWarehouseStockRepository();
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        await warehouse.SetAsync("default", MainWarehouse, "sku-1", 1m);
        await warehouse.SetAsync("DEFAULT", MainWarehouse, "SKU-1", 2m);

        WarehouseStockData data = Assert.Single(repository.Data);
        Assert.Equal("DEFAULT", data.StoreAlias);
        Assert.Equal("SKU-1", data.Sku);
        Assert.Equal(2m, data.Balance);
    }

    [Fact]
    public async Task ClearAsync_RemovesCaseVariantDuplicateRows()
    {
        var repository = new FakeWarehouseStockRepository();
        WarehouseStockData older = CreateData("default", MainWarehouse, "sku-1", 1m);
        WarehouseStockData newer = CreateData("DEFAULT", MainWarehouse, "SKU-1", 2m);
        newer.UpdateDate = older.UpdateDate.AddMinutes(1);
        repository.Data.Add(older);
        repository.Data.Add(newer);
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        bool cleared = await warehouse.ClearAsync(DefaultStore, MainWarehouse, "sku-1");
        cache.FillCache();
        WarehouseStockBalance? balance = await warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1");

        Assert.True(cleared);
        Assert.Empty(repository.Data);
        Assert.Null(balance);
    }

    [Fact]
    public async Task GetForSkuAsync_ReturnsVisibleDefinitionsWithNullableCachedBalances()
    {
        var definitions = new[]
        {
            new WarehouseDefinition
            {
                Key = SecondaryWarehouse,
                Code = "SECONDARY",
                Name = "Secondary",
                SortOrder = 20,
                Visible = true,
            },
            new WarehouseDefinition
            {
                Key = MainWarehouse,
                Code = "MAIN",
                Name = "Main",
                SortOrder = 10,
                Visible = true,
            },
            new WarehouseDefinition
            {
                Key = Guid.NewGuid(),
                Code = "HIDDEN",
                Name = "Hidden",
                SortOrder = 5,
                Visible = false,
            },
        };
        var repository = new FakeWarehouseStockRepository();
        repository.Data.Add(CreateData(DefaultStore, MainWarehouse, "SKU-1", 8m));
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache, definitions);
        cache.FillCache();

        IReadOnlyList<WarehouseStockLevel> levels = await warehouse.GetForSkuAsync(DefaultStore, "sku-1");

        Assert.Equal(2, levels.Count);
        Assert.Equal(MainWarehouse, levels[0].WarehouseKey);
        Assert.Equal(8m, levels[0].Balance);
        Assert.NotNull(levels[0].UpdateDate);
        Assert.Equal(SecondaryWarehouse, levels[1].WarehouseKey);
        Assert.Null(levels[1].Balance);
        Assert.Null(levels[1].UpdateDate);
    }

    [Fact]
    public async Task FailedRefresh_RetainsPreviousSnapshot()
    {
        var repository = new FakeWarehouseStockRepository();
        repository.Data.Add(CreateData(DefaultStore, MainWarehouse, "SKU-1", 5m));
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();
        repository.GetAllOverride = () => throw new InvalidOperationException("Refresh failed.");

        Assert.Throws<InvalidOperationException>(() => cache.FillCache());
        WarehouseStockBalance? balance = await warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1");

        Assert.Equal(5m, balance?.Balance);
    }

    [Fact]
    public async Task RefreshAndWrite_AreCoordinatedWithoutLosingNewerWrite()
    {
        var repository = new FakeWarehouseStockRepository();
        repository.Data.Add(CreateData(DefaultStore, MainWarehouse, "SKU-1", 1m));
        WarehouseApi warehouse = CreateWarehouse(repository, out WarehouseStockCache cache);
        cache.FillCache();

        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.GetAllOverride = async () =>
        {
            refreshStarted.SetResult();
            await releaseRefresh.Task.ConfigureAwait(false);
            return repository.Snapshot();
        };

        Task refresh = Task.Run(cache.FillCache);
        await refreshStarted.Task;
        Task<WarehouseStockBalance> write = warehouse.SetAsync(DefaultStore, MainWarehouse, "sku-1", 2m);
        Assert.False(write.IsCompleted);

        releaseRefresh.SetResult();
        await Task.WhenAll(refresh, write);
        WarehouseStockBalance? balance = await warehouse.GetAsync(DefaultStore, MainWarehouse, "sku-1");

        Assert.Equal(2m, balance?.Balance);
    }

    private static WarehouseApi CreateWarehouse(
        FakeWarehouseStockRepository repository,
        out WarehouseStockCache cache,
        IReadOnlyList<WarehouseDefinition>? definitions = null)
    {
        definitions ??=
        [
            new WarehouseDefinition
            {
                Key = MainWarehouse,
                Code = "MAIN",
                Name = "Main",
                SortOrder = 10,
                Visible = true,
            },
            new WarehouseDefinition
            {
                Key = SecondaryWarehouse,
                Code = "SECONDARY",
                Name = "Secondary",
                SortOrder = 20,
                Visible = true,
            },
        ];

        var definitionService = new Mock<IWarehouseDefinitionService>();
        definitionService
            .Setup(service => service.GetPublishedWarehouses(It.IsAny<string>()))
            .Returns(definitions);

        cache = new WarehouseStockCache(repository, NullLogger<WarehouseStockCache>.Instance);
        return new WarehouseApi(repository, definitionService.Object, cache);
    }

    private static WarehouseStockData CreateData(string storeAlias, Guid warehouseKey, string sku, decimal balance)
    {
        DateTime now = DateTime.UtcNow;
        return new WarehouseStockData
        {
            StoreAlias = storeAlias,
            WarehouseKey = warehouseKey,
            Sku = sku,
            Balance = balance,
            CreateDate = now,
            UpdateDate = now,
        };
    }

    private sealed class FakeWarehouseStockRepository : IWarehouseStockRepository
    {
        private readonly object _lock = new();

        public List<WarehouseStockData> Data { get; } = [];
        public HashSet<string> FailingSkus { get; } = new(StringComparer.Ordinal);
        public Func<Task<List<WarehouseStockData>>>? GetAllOverride { get; set; }
        public int GetAllCallCount { get; private set; }
        public int ApplyCallCount { get; private set; }

        public Task<List<WarehouseStockData>> GetAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            GetAllCallCount++;
            return GetAllOverride?.Invoke() ?? Task.FromResult(Snapshot());
        }

        public Task<WarehouseStockPersistenceBatchResult> ApplyAsync(
            IReadOnlyList<WarehouseStockPersistenceRequest> requests,
            CancellationToken ct = default)
        {
            ApplyCallCount++;
            var results = new List<WarehouseStockPersistenceResult>(requests.Count);

            lock (_lock)
            {
                foreach (WarehouseStockPersistenceRequest request in requests)
                {
                    ct.ThrowIfCancellationRequested();
                    if (FailingSkus.Contains(request.Sku))
                    {
                        results.Add(new WarehouseStockPersistenceResult(
                            request,
                            null,
                            new InvalidOperationException("Simulated persistence failure.")));
                        continue;
                    }

                    Data.RemoveAll(item => IsMatch(item, request));
                    if (request.Operation == WarehouseStockMutationOperation.Clear)
                    {
                        results.Add(new WarehouseStockPersistenceResult(request, null, null));
                        continue;
                    }

                    DateTime now = DateTime.UtcNow;
                    var data = new WarehouseStockData
                    {
                        StoreAlias = request.StoreAlias,
                        WarehouseKey = request.WarehouseKey,
                        Sku = request.Sku,
                        Balance = request.Balance.GetValueOrDefault(),
                        CreateDate = request.ExistingData?.CreateDate ?? now,
                        UpdateDate = now,
                    };
                    Data.Add(data);
                    results.Add(new WarehouseStockPersistenceResult(request, data, null));
                }
            }

            return Task.FromResult(new WarehouseStockPersistenceBatchResult(results, false));
        }

        public List<WarehouseStockData> Snapshot()
        {
            lock (_lock)
            {
                return Data.Select(item => new WarehouseStockData
                {
                    StoreAlias = item.StoreAlias,
                    WarehouseKey = item.WarehouseKey,
                    Sku = item.Sku,
                    Balance = item.Balance,
                    CreateDate = item.CreateDate,
                    UpdateDate = item.UpdateDate,
                }).ToList();
            }
        }

        private static bool IsMatch(WarehouseStockData item, WarehouseStockPersistenceRequest request)
        {
            return string.Equals(item.StoreAlias, request.StoreAlias, StringComparison.OrdinalIgnoreCase)
                && item.WarehouseKey == request.WarehouseKey
                && string.Equals(item.Sku, request.Sku, StringComparison.OrdinalIgnoreCase);
        }
    }
}
