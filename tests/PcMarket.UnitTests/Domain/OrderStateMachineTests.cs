using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.UnitTests.Domain;

public class OrderStateMachineTests
{
    private static Order NewOrder() => new()
    {
        Number = "ORD-1",
        UserId = Guid.NewGuid(),
        Status = OrderStatus.AwaitingPayment,
        ShippingAddress = new OrderAddress { Region = "Toshkent shahri", City = "Tashkent", Street = "Amir Temur 1" }
    };

    [Fact]
    public void TransitionTo_Paid_SetsPaymentStatus_AndRaisesEvents()
    {
        var order = NewOrder();

        order.TransitionTo(OrderStatus.Paid, "system");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Single(order.StatusHistory);
        Assert.Contains(order.DomainEvents, e => e is OrderStatusChangedEvent);
        Assert.Contains(order.DomainEvents, e => e is OrderPaidEvent);
    }

    [Fact]
    public void TransitionTo_IllegalTarget_Throws()
    {
        var order = NewOrder();

        var ex = Assert.Throws<DomainException>(() => order.TransitionTo(OrderStatus.Delivered));
        Assert.Contains("Illegal order transition", ex.Message);
    }

    [Fact]
    public void TransitionTo_SameStatus_IsNoOp()
    {
        var order = NewOrder();

        order.TransitionTo(OrderStatus.AwaitingPayment);

        Assert.Empty(order.StatusHistory);
        Assert.Empty(order.DomainEvents);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    [InlineData(OrderStatus.Refunded, OrderStatus.Processing)]
    public void CanTransitionTo_FromTerminalStates_IsFalse(OrderStatus from, OrderStatus to)
    {
        var order = NewOrder();
        order.Status = from;

        Assert.False(order.CanTransitionTo(to));
    }
}
