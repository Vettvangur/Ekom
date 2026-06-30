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

internal sealed class MigrationCreateTables : MigrationBase
{
    private readonly DatabaseService _databaseService;

    public MigrationCreateTables(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

    protected override void Migrate()
    {
        _databaseService.CreateTables();
    }
}

internal sealed class MigrationAddOrderActivityLogTypeColumn : MigrationBase
{
    private readonly DatabaseService _databaseService;

    public MigrationAddOrderActivityLogTypeColumn(DatabaseService databaseService, IMigrationContext context)
        : base(context)
    {
        _databaseService = databaseService;
    }

    protected override void Migrate()
    {
        _databaseService.EnsureOrderActivityLogTypeColumn();
    }
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
    }
}

internal sealed class EnsureTablesExist : IComponent
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

    public void Initialize()
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
            ExecuteMigrationPlan();
            _keyValueService.SetValue("Umbraco.Core.Upgrader.State+Ekom", "2");
        }
        else if (currentState == "1")
        {
            _logger.LogInformation("Running Ekom database activity log type migration.");
            ExecuteMigrationPlan();
            _keyValueService.SetValue("Umbraco.Core.Upgrader.State+Ekom", "2");
        }
        else
        {
            _databaseService.CreateTables();
        }

        _logger.LogDebug("Done");
    }

    public void Terminate()
    {
    }

    private void ExecuteMigrationPlan()
    {
        var upgrader = new Upgrader(new EkomMigrationPlan());
        upgrader.Execute(_migrationPlanExecutor, _scopeProvider, _keyValueService);
    }
}
