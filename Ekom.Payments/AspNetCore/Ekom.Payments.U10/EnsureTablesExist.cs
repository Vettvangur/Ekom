
#pragma warning disable CA1707 // Identifiers should not contain underscores
namespace Ekom.Payments.App_Start;
#pragma warning restore CA1707 // Identifiers should not contain underscores

///// <summary>
///// Dummy component to represent the previous incompatible migration
///// </summary>
//class DummyComponent : MigrationBase
//{
//    public DummyComponent(IMigrationContext context)
//        : base(context)
//    {
//    }

//    public override void Migrate() { }
//}

//class MigrationCreateTables : MigrationBase
//{
//    readonly ILogger _logger;

//    public MigrationCreateTables(
//        IMigrationContext context,
//        ILogger logger)
//        : base(context)
//    {
//        _logger = logger;
//    }

//    public override void Migrate()
//    {
//        if (!TableExists(TableInfo.FromPoco(typeof(OrderStatus)).TableName))
//        {
//            _logger.Debug<MigrationCreateTables>(
//                "Creating {TableName} table",
//                TableInfo.FromPoco(typeof(OrderStatus)).TableName);

//            Create.Table<OrderStatus>().Do();
//            Execute.Sql($"CREATE UNIQUE NONCLUSTERED INDEX [{TranslationMigrationPlan.OrderStatusUniqueIndex}] ON {TableInfo.FromPoco(typeof(OrderStatus)).TableName} ( [ReferenceId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]").Do();
//        }
//        else
//        {
//            throw new System.Exception("NetPayment table exists, this NetPayment upgrade cannot upgrade in-place, please rename your old tables so that new ones can be created");
//        }
//        if (!TableExists(TableInfo.FromPoco(typeof(PaymentData)).TableName))
//        {
//            _logger.Debug<MigrationCreateTables>(
//                "Creating {TableName} table",
//                TableInfo.FromPoco(typeof(PaymentData)).TableName);

//            Create.Table<PaymentData>().Do();
//        }
//    }
//}

//class MigrationExpandNetPaymentData : MigrationBase
//{
//    public MigrationExpandNetPaymentData(IMigrationContext context)
//        : base(context)
//    {
//    }

//    public override void Migrate() 
//    {
//        Execute.Sql(
//            $"ALTER TABLE {TableInfo.FromPoco(typeof(OrderStatus)).TableName} ALTER COLUMN {nameof(OrderStatus.NetPaymentData)} nvarchar(MAX) NULL").Do();
//    }
//}

//class TranslationMigrationPlan : MigrationPlan
//{
//    public const string OrderStatusUniqueIndex = "IX_NetPaymentOrders_UniqueId";
//    public TranslationMigrationPlan()
//        : base("NetPayment")
//    {
//        From(string.Empty)
//            .To<DummyComponent>("first-migration")
//            .To<MigrationCreateTables>("v1.0")
//            .To<MigrationExpandNetPaymentData>("v1.1")
//            ;
//    }
//}

//class EnsureTablesExist : IComponent
//{
//    private readonly IScopeProvider scopeProvider;
//    private readonly IMigrationBuilder migrationBuilder;
//    private readonly IKeyValueService keyValueService;
//    private readonly ILogger logger;

//    public EnsureTablesExist(
//        IScopeProvider scopeProvider,
//        IMigrationBuilder migrationBuilder,
//        IKeyValueService keyValueService,
//        ILogger logger)
//    {
//        this.scopeProvider = scopeProvider;
//        this.migrationBuilder = migrationBuilder;
//        this.keyValueService = keyValueService;
//        this.logger = logger;
//    }

//    public void Initialize()
//    {
//        logger.Debug<EnsureTablesExist>("Ensuring NetPayment db tables exist");

//        // perform any upgrades (as needed)
//        var upgrader = new Upgrader(new TranslationMigrationPlan());
//        upgrader.Execute(scopeProvider, migrationBuilder, keyValueService, logger);
//        logger.Debug<EnsureTablesExist>("Done");
//    }

//    public void Terminate()
//    {
//    }
//}
