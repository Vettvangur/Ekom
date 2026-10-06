using Ekom.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Data;
using System.Reflection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Xunit;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Ekom.Tests.Tests;

public sealed class OrderPerformanceMigrationPlanTests
{
    private const string FinalState = "order-performance-indexes-v1";
    private const string StateKey = "Umbraco.Core.Upgrader.State+Ekom";

#if NET8_0
    private const string AssemblyName = "Ekom.U10";
    private const string MigrationNamespace = "Ekom.App_Start";
#else
    private const string AssemblyName = "Ekom.U17";
    private const string MigrationNamespace = "Ekom.Umb";
#endif

    public static IEnumerable<object[]> LegacyStates()
    {
        yield return new object[] { string.Empty };
        yield return new object[] { "1" };
        yield return new object[] { "2" };
#if NET10_0
        yield return new object[] { "3" };
#endif
        yield return new object[] { "native-reservations-v1" };
        yield return new object[] { "orders-indexes-v1" };
    }

    [Theory]
    [MemberData(nameof(LegacyStates))]
    public void FreshAndLegacyStatesFollowTheActualPlanToPerformanceIndexes(string initialState)
    {
        var plan = CreatePlan();
        string[] states =
        {
            string.Empty, "1", "2",
#if NET10_0
            "3",
#endif
            "native-reservations-v1", "orders-indexes-v1", FinalState,
        };
        var actual = new List<string> { initialState };
        var state = initialState;

        plan.Validate();
        while (state != FinalState)
        {
            Assert.True(plan.Transitions.TryGetValue(state, out var transition));
            Assert.NotNull(transition);
            Assert.Equal(state, transition.SourceState);
            Assert.DoesNotContain(transition.TargetState, actual);
            state = transition.TargetState;
            actual.Add(state);
        }

        Assert.Equal(states.Skip(Array.IndexOf(states, initialState)), actual);
        Assert.Equal(FinalState, plan.FinalState);
        Assert.Null(plan.Transitions[FinalState]);
    }

    [Fact]
    public void PhaseOneIndexMigrationIsPreservedBeforePerformanceIndexMigration()
    {
        var plan = CreatePlan();
        var phaseOne = Assert.IsType<MigrationPlan.Transition>(plan.Transitions["native-reservations-v1"]);
        var performance = Assert.IsType<MigrationPlan.Transition>(plan.Transitions["orders-indexes-v1"]);

        Assert.Equal("orders-indexes-v1", phaseOne.TargetState);
        Assert.Equal(MigrationType("MigrationEnsureOrderIndexes"), phaseOne.MigrationType);
        Assert.Equal(FinalState, performance.TargetState);
        Assert.Equal(MigrationType("MigrationEnsureOrderPerformanceIndexes"), performance.MigrationType);
    }

    [Theory]
    [MemberData(nameof(LegacyStates))]
    public async Task ComponentRoutesLegacyStateThroughTheActualPlanAndCompletesOnSuccess(string initialState)
    {
        var fixture = new ComponentFixture(initialState, successful: true);

        await fixture.InitializeAsync();

        fixture.VerifyExecuted(initialState);
#if NET8_0
        Assert.Equal(FinalState, fixture.StoredState);
        fixture.KeyValues.Verify(x => x.SetValue(StateKey, FinalState), Times.Once);
#else
        // Umbraco 17's executor owns persistence; Upgrader only returns its result.
        // A mocked executor cannot verify that database-backed persistence behavior.
        fixture.KeyValues.Verify(x => x.SetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
#endif
        fixture.Database.Verify(x => x.CreateTables(), Times.Never);
        fixture.Database.Verify(x => x.EnsureStockReservationTable(), Times.Once);
#if NET8_0
        fixture.Database.Verify(x => x.EnsureOrderActivityLogTypeColumn(), Times.Once);
#endif
    }

    [Theory]
    [MemberData(nameof(LegacyStates))]
    public async Task UnsuccessfulResultPropagatesInnerExceptionWithoutMarkingNewState(string initialState)
    {
        var fixture = new ComponentFixture(initialState, successful: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync());

        Assert.Same(fixture.Failure, exception.InnerException);
        Assert.Contains($"from state '{initialState}' at state '{initialState}'", exception.Message, StringComparison.Ordinal);
        fixture.VerifyExecuted(initialState);
        Assert.Equal(initialState, fixture.StoredState);
        fixture.KeyValues.Verify(x => x.SetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        fixture.Database.Verify(x => x.CreateTables(), Times.Never);
        fixture.Database.Verify(x => x.EnsureStockReservationTable(), Times.Never);
        fixture.Database.Verify(x => x.EnsureOrderActivityLogTypeColumn(), Times.Never);
    }

    private static Type MigrationType(string name) =>
        Assembly.Load(AssemblyName).GetType($"{MigrationNamespace}.{name}", throwOnError: true)!;

    private static MigrationPlan CreatePlan() =>
        (MigrationPlan)Activator.CreateInstance(MigrationType("EkomMigrationPlan"))!;

    private sealed class ComponentFixture
    {
        private readonly object _component;
        private readonly Mock<IMigrationPlanExecutor> _executor = new(MockBehavior.Strict);

        public ComponentFixture(string initialState, bool successful)
        {
            StoredState = initialState;
            KeyValues.Setup(x => x.GetValue(StateKey)).Returns(() => StoredState);
            KeyValues.Setup(x => x.SetValue(StateKey, It.IsAny<string>()))
                .Callback<string, string>((_, value) => StoredState = value);

            var scope = new Mock<ICoreScope>();
            var scopeProvider = new Mock<IScopeProvider>();
            scopeProvider.As<ICoreScopeProvider>()
                .Setup(x => x.CreateCoreScope(IsolationLevel.Unspecified, RepositoryCacheMode.Unspecified,
                    null, null, null, false, false))
                .Returns(scope.Object);
            var runtime = new Mock<IRuntimeState>();
            runtime.SetupGet(x => x.Level).Returns(RuntimeLevel.Run);

#if NET8_0
            _executor.Setup(x => x.ExecutePlan(It.IsAny<MigrationPlan>(), initialState))
                .Returns((MigrationPlan plan, string state) => Result(plan, state, successful));
#else
            _executor.Setup(x => x.ExecutePlanAsync(It.IsAny<MigrationPlan>(), initialState))
                .ReturnsAsync((MigrationPlan plan, string state) => Result(plan, state, successful));
#endif
            var componentType = MigrationType("EnsureTablesExist");
            var loggerType = typeof(NullLogger<>).MakeGenericType(componentType);
            var logger = Activator.CreateInstance(loggerType);
            _component = Activator.CreateInstance(componentType,
                scopeProvider.Object, KeyValues.Object, logger, _executor.Object, runtime.Object, Database.Object)!;
        }

        public string StoredState { get; private set; }
        public Exception Failure { get; } = new InvalidOperationException("Deliberate test migration failure");
        public Mock<IKeyValueService> KeyValues { get; } = new(MockBehavior.Strict);
        public Mock<DatabaseService> Database { get; } = new(
            MockBehavior.Strict, null!, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());

        public async Task InitializeAsync()
        {
            Database.Setup(x => x.EnsureStockReservationTable());
            Database.Setup(x => x.EnsureOrderActivityLogTypeColumn());
#if NET8_0
            try
            {
                _component.GetType().GetMethod("Initialize")!.Invoke(_component, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            await Task.CompletedTask;
#else
            var task = (Task)_component.GetType().GetMethod("InitializeAsync")!
                .Invoke(_component, new object[] { false, CancellationToken.None })!;
            await task;
#endif
        }

        public void VerifyExecuted(string initialState)
        {
#if NET8_0
            _executor.Verify(x => x.ExecutePlan(
#else
            _executor.Verify(x => x.ExecutePlanAsync(
#endif
                It.Is<MigrationPlan>(plan => plan.GetType() == MigrationType("EkomMigrationPlan") && plan.FinalState == FinalState),
                initialState), Times.Once);
            _executor.VerifyNoOtherCalls();
        }

        private ExecutedMigrationPlan Result(MigrationPlan plan, string initialState, bool successful) => new()
        {
            Plan = plan,
            InitialState = initialState,
            FinalState = successful ? FinalState : initialState,
            Successful = successful,
            Exception = successful ? null : Failure,
            CompletedTransitions = Array.Empty<MigrationPlan.Transition>(),
        };
    }
}
