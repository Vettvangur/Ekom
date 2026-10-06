using Ekom.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.App_Start;

class MigrationCreateTables : MigrationBase
{

    readonly ILogger _logger;
    readonly Configuration _config;
    readonly DatabaseService _dbService;
    public MigrationCreateTables(
        ILogger<MigrationCreateTables> logger,
        Configuration configuration,
        DatabaseService dbService,
        IMigrationContext context)
        : base(context)
    {
        _logger = logger;
        _config = configuration;
        _dbService = dbService;
    }

    protected override void Migrate()
    {
        _dbService.CreateTables();
    }
}

class MigrationAddOrderActivityLogTypeColumn : MigrationBase
{
    readonly DatabaseService _dbService;

    public MigrationAddOrderActivityLogTypeColumn(
        DatabaseService dbService,
        IMigrationContext context)
        : base(context)
    {
        _dbService = dbService;
    }

    protected override void Migrate()
    {
        _dbService.EnsureOrderActivityLogTypeColumn();
    }
}

class MigrationCreateStockReservationTable : MigrationBase
{
    private readonly DatabaseService _databaseService;
    public MigrationCreateStockReservationTable(DatabaseService databaseService, IMigrationContext context) : base(context)
        => _databaseService = databaseService;
    protected override void Migrate() => _databaseService.EnsureStockReservationTable();
}

class MigrationEnsureOrderIndexes : MigrationBase
{
    private readonly DatabaseService _databaseService;

    public MigrationEnsureOrderIndexes(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

    protected override void Migrate() => _databaseService.EnsureOrderIndexes();
}

class EkomMigrationPlan : MigrationPlan
{
    public const string OrderDataUniqueIndex = "IX_EkomOrders_UniqueId";

    public EkomMigrationPlan()
         : base("Ekom")
    {
        From(string.Empty)
            .To<MigrationCreateTables>("1"); // Run only if the state is empty

        From("1")
            .To<MigrationAddOrderActivityLogTypeColumn>("2");

        From("2").To<MigrationCreateStockReservationTable>("native-reservations-v1");
        From("native-reservations-v1").To<MigrationEnsureOrderIndexes>("orders-indexes-v1");
    }
}

class EnsureTablesExist : IComponent
{
    private readonly IScopeProvider scopeProvider;
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly IKeyValueService keyValueService;
    private readonly ILogger logger;
    private readonly IRuntimeState _runtimeState;
    private readonly DatabaseService _dbService;

    public EnsureTablesExist(
        IScopeProvider scopeProvider,
        IKeyValueService keyValueService,
        ILogger<EnsureTablesExist> logger,
        IMigrationPlanExecutor migrationPlanExecutor,
        IRuntimeState runtimeState,
        DatabaseService dbService)
    {
        this.scopeProvider = scopeProvider;
        this.keyValueService = keyValueService;
        this.logger = logger;
        _migrationPlanExecutor = migrationPlanExecutor;
        _runtimeState = runtimeState;
        _dbService = dbService;
    }

    public void Initialize()
    {
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            // If Installing or Upgrading, we don't want to run this
            return;
        }

        logger.LogDebug("Ensuring Ekom db tables exist");

        var currentState = keyValueService.GetValue("Umbraco.Core.Upgrader.State+Ekom");

        if (string.IsNullOrEmpty(currentState))
        {
            logger.LogInformation("Running initial database setup for Ekom.");

            ExecuteMigrationPlan();
        }
        else if (currentState == "1")
        {
            logger.LogInformation("Running Ekom database activity log type migration.");

            ExecuteMigrationPlan();
        }
        else if (currentState == "2" || currentState == "native-reservations-v1")
        {
            ExecuteMigrationPlan();
        }
        else
        {
            _dbService.CreateTables();
        }

        _dbService.EnsureOrderActivityLogTypeColumn();
        _dbService.EnsureStockReservationTable();

        logger.LogDebug("Done");
    }

    public void Terminate() { }

    private void ExecuteMigrationPlan()
    {
        var upgrader = new Upgrader(new EkomMigrationPlan());
        var result = upgrader.Execute(_migrationPlanExecutor, scopeProvider, keyValueService);
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Ekom database migration failed from state '{result.InitialState}' at state '{result.FinalState}'. See the inner exception for detailed diagnostics.",
                result.Exception);
        }
    }
}
