namespace PcMarket.Contracts.Orders;

/// <summary>Wire representations of the order/payment enums. Values mirror the domain enums so a plain
/// numeric cast maps between them; serialized as strings for readable clients and OpenAPI.</summary>
public enum OrderStatus
{
    Created = 0,
    AwaitingPayment = 1,
    Paid = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Refunded = 7
}

public enum PaymentStatus
{
    None = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

public enum PaymentMethod
{
    Cash = 0,
    Click = 1,
    Payme = 2,
    Uzcard = 3,
    Humo = 4
}

public enum DeliveryType
{
    Courier = 0,
    Pickup = 1
}
