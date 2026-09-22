using Ekom.Events;
using Ekom.Models;
using Ekom.Utilities;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderStatusEventArgsTests
{
    [Fact]
    public void OrderStatusEventArgs_Exposes_OrderInfo()
    {
        var orderInfo = new Mock<IOrderInfo>().Object;
        var args = new OrderStatusEventArgs
        {
            OrderUniqueId = Guid.NewGuid(),
            OrderInfo = orderInfo,
            PreviousStatus = OrderStatus.Incomplete,
            Status = OrderStatus.ReadyForDispatch
        };

        Assert.Same(orderInfo, args.OrderInfo);
    }
}
