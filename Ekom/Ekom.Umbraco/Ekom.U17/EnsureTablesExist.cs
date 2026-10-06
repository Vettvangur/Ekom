using Ekom.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb;

#if UMBRACO_18
internal sealed class MigrationCreateTables : AsyncMigrationBase
#else
internal sealed class MigrationCreateTables : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;

    public MigrationCreateTables(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.CreateTables();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate()
    {
        _databaseService.CreateTables();
    }
#endif
}

#if UMBRACO_18
internal sealed class MigrationAddOrderActivityLogTypeColumn : AsyncMigrationBase
#else
internal sealed class MigrationAddOrderActivityLogTypeColumn : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;

    public MigrationAddOrderActivityLogTypeColumn(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.EnsureOrderActivityLogTypeColumn();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate()
    {
        _databaseService.EnsureOrderActivityLogTypeColumn();
    }
#endif
}

#if UMBRACO_18
internal sealed class MigrationCreateWarehouseStockTable : AsyncMigrationBase
#else
internal sealed class MigrationCreateWarehouseStockTable : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;

    public MigrationCreateWarehouseStockTable(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.EnsureWarehouseStockTable();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate()
    {
        _databaseService.EnsureWarehouseStockTable();
    }
#endif
}

#if UMBRACO_18
internal sealed class MigrationCreateStockReservationTable : AsyncMigrationBase
#else
internal sealed class MigrationCreateStockReservationTable : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;
    public MigrationCreateStockReservationTable(DatabaseService databaseService, IMigrationContext context) : base(context)
        => _databaseService = databaseService;
#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.EnsureStockReservationTable();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate() => _databaseService.EnsureStockReservationTable();
#endif
}

#if UMBRACO_18
internal sealed class MigrationEnsureOrderIndexes : AsyncMigrationBase
#else
internal sealed class MigrationEnsureOrderIndexes : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;

    public MigrationEnsureOrderIndexes(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.EnsureOrderIndexes();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate() => _databaseService.EnsureOrderIndexes();
#endif
}

#if UMBRACO_18
internal sealed class MigrationEnsureOrderPerformanceIndexes : AsyncMigrationBase
#else
internal sealed class MigrationEnsureOrderPerformanceIndexes : MigrationBase
#endif
{
    private readonly DatabaseService _databaseService;

    public MigrationEnsureOrderPerformanceIndexes(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

#if UMBRACO_18
    protected override Task MigrateAsync()
    {
        _databaseService.EnsureOrderPerformanceIndexes();
        return Task.CompletedTask;
    }
#else
    protected override void Migrate() => _databaseService.EnsureOrderPerformanceIndexes();
#endif
}

internal sealed class EkomMigrationPlan : MigrationPlan
{
    public EkomMigrationPlan()
        : base("Ekom")
    {
        From(string.Empty)
            .To<MigrationCreateTables>("1");

        From("1")
            .To<MigrationAddOrderActivityLogTypeColumn>("2");

        From("2")
            .To<MigrationCreateWarehouseStockTable>("3");
        From("3").To<MigrationCreateStockReservationTable>("native-reservations-v1");
        From("native-reservations-v1").To<MigrationEnsureOrderIndexes>("orders-indexes-v1");
        From("orders-indexes-v1").To<MigrationEnsureOrderPerformanceIndexes>("order-performance-indexes-v1");
    }
}

internal sealed class EnsureTablesExist : IAsyncComponent
{
    private readonly IScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly IKeyValueService _keyValueService;
    private readonly ILogger<EnsureTablesExist> _logger;
    private readonly IRuntimeState _runtimeState;
    private readonly DatabaseService _databaseService;

    public EnsureTablesExist(
        IScopeProvider scopeProvider,
        IKeyValueService keyValueService,
        ILogger<EnsureTablesExist> logger,
        IMigrationPlanExecutor migrationPlanExecutor,
        IRuntimeState runtimeState,
        DatabaseService databaseService)
    {
        _scopeProvider = scopeProvider;
        _keyValueService = keyValueService;
        _logger = logger;
        _migrationPlanExecutor = migrationPlanExecutor;
        _runtimeState = runtimeState;
        _databaseService = databaseService;
    }

    public async Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            return;
        }

        _logger.LogDebug("Ensuring Ekom db tables exist");

        var currentState = _keyValueService.GetValue("Umbraco.Core.Upgrader.State+Ekom");

        if (string.IsNullOrEmpty(currentState))
        {
            _logger.LogInformation("Running initial database setup for Ekom.");
            await ExecuteMigrationPlanAsync().ConfigureAwait(false);
        }
        else if (currentState == "1")
        {
            _logger.LogInformation("Running Ekom database activity log type migration.");
            await ExecuteMigrationPlanAsync().ConfigureAwait(false);
        }
        else if (currentState == "2")
        {
            _logger.LogInformation("Running Ekom warehouse stock migration.");
            await ExecuteMigrationPlanAsync().ConfigureAwait(false);
        }
        else if (currentState == "3" || currentState == "native-reservations-v1" || currentState == "orders-indexes-v1")
        {
            await ExecuteMigrationPlanAsync().ConfigureAwait(false);
        }
        else
        {
            _databaseService.CreateTables();
        }

        _databaseService.EnsureStockReservationTable();
        _logger.LogDebug("Done");
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task ExecuteMigrationPlanAsync()
    {
        var upgrader = new Upgrader(new EkomMigrationPlan());
        var result = await upgrader.ExecuteAsync(_migrationPlanExecutor, _scopeProvider, _keyValueService).ConfigureAwait(false);
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Ekom database migration failed from state '{result.InitialState}' at state '{result.FinalState}'. See the inner exception for detailed diagnostics.",
                result.Exception);
        }
    }
}
