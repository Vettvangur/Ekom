using Ekom.Models.Data;
using NPoco;
using System;
using System.Collections.Generic;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Migrations;
using Umbraco.Core.Migrations.Upgrade;
using Umbraco.Core.Persistence;
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
            if (!TableExists(TableInfo.FromPoco(typeof(StockData)).TableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    TableInfo.FromPoco(typeof(StockData)).TableName);

                Create.Table<StockData>().Do();
            }
            if (!TableExists(TableInfo.FromPoco(typeof(OrderActivityLog)).TableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    TableInfo.FromPoco(typeof(OrderActivityLog)).TableName);

                Create.Table<OrderActivityLog>().Do();
            }
            if (!TableExists(TableInfo.FromPoco(typeof(OrderData)).TableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    TableInfo.FromPoco(typeof(OrderData)).TableName);

                Create.Table<OrderData>().Do();
                Execute.Sql($"ALTER TABLE {TableInfo.FromPoco(typeof(OrderData)).TableName} ALTER COLUMN OrderInfo NVARCHAR(MAX)").Do();
                Execute.Sql($"CREATE CLUSTERED INDEX [{EkomMigrationPlan.OrderDataUniqueIndex}] ON {TableInfo.FromPoco(typeof(OrderData)).TableName} ( [UniqueId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]").Do();
            }
            if (!TableExists(TableInfo.FromPoco(typeof(CouponData)).TableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    TableInfo.FromPoco(typeof(CouponData)).TableName);

                Create.Table<CouponData>().Do();
            }
            if (!TableExists(Configuration.DiscountStockTableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    Configuration.DiscountStockTableName);

                Create.Table<DiscountStockData>().Do();
            }
            if (_config.StoreCustomerData
            && !TableExists(TableInfo.FromPoco(typeof(CustomerData)).TableName))
            {
                _logger.Info<MigrationCreateTables>(
                    "Creating {TableName} table",
                    TableInfo.FromPoco(typeof(CustomerData)).TableName);

                Create.Table<CustomerData>().Do();
            }
        }
    }

    class MigrationUpdatev2 : MigrationBase
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        public MigrationUpdatev2(
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
            Execute.Sql($"DROP INDEX [{EkomMigrationPlan.OrderDataUniqueIndex}] ON {TableInfo.FromPoco(typeof(OrderData)).TableName} WITH ( ONLINE = OFF )").Do();
            Execute.Sql($"CREATE UNIQUE CLUSTERED INDEX [{EkomMigrationPlan.OrderDataUniqueIndex}] ON {TableInfo.FromPoco(typeof(OrderData)).TableName} ( [UniqueId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]").Do();
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
                .To<MigrationUpdatev2>("2");
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
            var upgrader = new Upgrader(new EkomMigrationPlan());
            upgrader.Execute(scopeProvider, migrationBuilder, keyValueService, logger);

            logger.Debug<EnsureTablesExist>("Done");
        }

        public void Terminate() { }
    }
}
