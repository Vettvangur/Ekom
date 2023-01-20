using Ekom.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.App_Start
{
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
                .To<MigrationCreateTables>("1")
                //.To<MigrationUpdatev2>("2")
                ;
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

            // perform any upgrades (as needed)
            var upgrader = new Upgrader(new EkomMigrationPlan());
            upgrader.Execute(_migrationPlanExecutor, scopeProvider, keyValueService);

            logger.LogDebug("Done");
        }

        public void Terminate() { }
    }
}
