using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

namespace Ekom.App_Start
{
    class MigrationCreateTables : MigrationBase
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        public MigrationCreateTables(
            ILogger logger,
            Configuration configuration,
            IMigrationContext context)
            : base(context)
        {
            _logger = logger;
            _config = configuration;
        }

        public override void Migrate()
        {
            if (!TableExists("EkomStock"))
            {
                _logger.Debug<MigrationCreateTables>("Creating EkomStock table");

                Create.Table<StockData>().Do();
            }
            if (!TableExists("EkomOrdersActivityLog"))
            {
                _logger.Debug<MigrationCreateTables>("Creating EkomOrdersActivityLog table");

                Create.Table<OrderActivityLog>().Do();
            }
            if (!TableExists("EkomOrders"))
            {
                _logger.Debug<MigrationCreateTables>("Creating EkomOrders table");

                Create.Table<OrderData>().Do();
                using (var db = Context.Database)
                {
                    db.Execute("ALTER TABLE EkomOrders ALTER COLUMN OrderInfo NVARCHAR(MAX)");
                }
            }
            if (!TableExists("EkomCoupon"))
            {
                _logger.Debug<MigrationCreateTables>("Creating EkomCoupon table");

                Create.Table<CouponData>().Do();
            }
            if (!TableExists(Configuration.DiscountStockTableName))
            {
                _logger.Debug<MigrationCreateTables>($"Creating {Configuration.DiscountStockTableName} table");

                Create.Table<DiscountStockData>().Do();
            }
            if (_config.StoreCustomerData
            && !TableExists("EkomCustomerData"))
            {
                _logger.Debug<MigrationCreateTables>("Creating EkomCustomerData table");

                Create.Table<CustomerData>().Do();
            }
        }
    }

    class TranslationMigrationPlan : MigrationPlan
    {
        public TranslationMigrationPlan()
            : base("MyApplicationName")
        {
            From(string.Empty)
                .To<MigrationCreateTables>("first-migration");
        }
    }

    class EnsureTablesExist : IComponent
    {
        private readonly IScopeProvider scopeProvider;
        private readonly IMigrationBuilder migrationBuilder;
        private readonly IKeyValueService keyValueService;
        private readonly ILogger logger;

        public EnsureTablesExist(
            IScopeProvider scopeProvider,
            IMigrationBuilder migrationBuilder,
            IKeyValueService keyValueService,
            ILogger logger)
        {
            this.scopeProvider = scopeProvider;
            this.migrationBuilder = migrationBuilder;
            this.keyValueService = keyValueService;
            this.logger = logger;
        }

        public void Initialize()
        {
            logger.Debug<EnsureTablesExist>("Ensuring Ekom db tables exist");

            // perform any upgrades (as needed)
            var upgrader = new Upgrader(new TranslationMigrationPlan());
            upgrader.Execute(scopeProvider, migrationBuilder, keyValueService, logger);
            logger.Debug<EnsureTablesExist>("Done");
        }

        public void Terminate()
        {
        }
    }
}
