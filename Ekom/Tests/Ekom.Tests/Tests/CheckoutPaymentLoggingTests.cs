using Ekom.Controllers;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class CheckoutPaymentLoggingTests
{
    [Fact]
    public void LogError_IncludesResolvedOrderAndExceptionInMessageAndStructuredFields()
    {
        var logger = new TestLogger<EkomCheckoutApiController>();
        var context = new CheckoutPaymentLogContext();
        var order = new Mock<IOrderInfo>();
        var id = Guid.NewGuid();
        order.SetupGet(x => x.OrderNumber).Returns("IS-1234");
        order.SetupGet(x => x.UniqueId).Returns(id);
        context.Capture(order.Object);
        var exception = new InvalidOperationException("Payment declined");

        context.LogError(logger, exception);

        Assert.Same(exception, logger.Exception);
        Assert.Equal(LogLevel.Error, logger.Level);
        Assert.Contains(exception.Message, logger.Message, StringComparison.Ordinal);
        Assert.Contains("IS-1234", logger.Message, StringComparison.Ordinal);
        Assert.Contains(id.ToString(), logger.Message, StringComparison.Ordinal);
        Assert.Equal(exception.Message, logger.Fields["ErrorMessage"]);
        Assert.Equal("IS-1234", logger.Fields["OrderNumber"]);
        Assert.Equal(id, logger.Fields["OrderUniqueId"]);
    }

    [Fact]
    public async Task ApiPay_LogsEarlyFailureWithoutOrderAndRethrowsOriginalException()
    {
        var logger = new TestLogger<EkomCheckoutApiController>();
        var controller = new EkomCheckoutApiController(logger, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var exception = await Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(() => controller.Pay("en-US"));

        Assert.Same(exception, logger.Exception);
        Assert.Contains(exception.Message, logger.Message, StringComparison.Ordinal);
        Assert.Null(logger.Fields["OrderNumber"]);
        Assert.Null(logger.Fields["OrderUniqueId"]);
    }

    [Fact]
    public async Task MvcPay_LogsEarlyFailureWithoutOrderAndPreservesErrorRedirect()
    {
        var logger = new TestLogger<CheckoutController>();
        var controller = new CheckoutController(logger, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<RedirectResult>(await controller.Pay(new PaymentRequest()));

        Assert.Equal("?errorStatus=serverError", result.Url);
        Assert.NotNull(logger.Exception);
        Assert.Contains(logger.Exception.Message, logger.Message, StringComparison.Ordinal);
        Assert.Null(logger.Fields["OrderNumber"]);
        Assert.Null(logger.Fields["OrderUniqueId"]);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public Exception? Exception { get; private set; }
        public LogLevel Level { get; private set; }
        public string Message { get; private set; } = "";
        public Dictionary<string, object?> Fields { get; private set; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
            Message = formatter(state, exception);
            Fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
