using Ekom.Services;
using Microsoft.Extensions.Logging;
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

class EkomMigrationPlan : MigrationPlan
{
    public const string OrderDataUniqueIndex = "IX_EkomOrders_UniqueId";

    public EkomMigrationPlan()
         : base("Ekom")
    {
        From(string.Empty)
            .To<MigrationCreateTables>("1"); // Run only if the state is empty
    }
}

class EnsureTablesExist : IComponent
{
    private readonly IScopeProvider scopeProvider;
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly IKeyValueService keyValueService;
    private readonly ILogger logger;

    public EnsureTablesExist(
        IScopeProvider scopeProvider,
        IKeyValueService keyValueService,
        ILogger<EnsureTablesExist> logger,
        IMigrationPlanExecutor migrationPlanExecutor)
    {
        this.scopeProvider = scopeProvider;
        this.keyValueService = keyValueService;
        this.logger = logger;
        _migrationPlanExecutor = migrationPlanExecutor;
    }

    public void Initialize()
    {
        logger.LogDebug("Ensuring Ekom db tables exist");

        var currentState = keyValueService.GetValue("Umbraco.Core.Upgrader.State+Ekom");

        if (string.IsNullOrEmpty(currentState)) // Run only if the state is empty
        {
            logger.LogInformation("Running initial database setup for Ekom.");

            var upgrader = new Upgrader(new EkomMigrationPlan());
            upgrader.Execute(_migrationPlanExecutor, scopeProvider, keyValueService);

            // Mark as complete so it never runs again
            keyValueService.SetValue("Umbraco.Core.Upgrader.State+Ekom", "1");
        }

        logger.LogDebug("Done");
    }

    public void Terminate() { }
}
