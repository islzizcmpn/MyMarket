namespace PcMarket.Domain.Enums;

/// <summary>Lifecycle states of an <c>Order</c>.</summary>
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

/// <summary>Aggregate payment state stamped on an order.</summary>
public enum PaymentStatus
{
    None = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

/// <summary>Payment method chosen by the customer at checkout.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Click = 1,
    Payme = 2,
    Uzcard = 3,
    Humo = 4
}

/// <summary>How an order is delivered.</summary>
public enum DeliveryType
{
    Courier = 0,
    Pickup = 1
}

/// <summary>Concrete payment rail behind a <c>PaymentTransaction</c>.</summary>
public enum PaymentProvider
{
    Cash = 0,
    Click = 1,
    Payme = 2,
    Uzcard = 3,
    Humo = 4
}

/// <summary>State of a single gateway transaction in the payment ledger.</summary>
public enum PaymentTransactionState
{
    Created = 0,
    Pending = 1,
    Performed = 2,
    Cancelled = 3,
    Failed = 4
}

/// <summary>Delivery channel for an outbound notification.</summary>
public enum NotificationChannel
{
    Telegram = 0,
    Push = 1,
    Sms = 2,
    Email = 3
}

/// <summary>Delivery state of a notification.</summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

/// <summary>Client platform a push registration token was issued for.</summary>
public enum DevicePlatform
{
    Android = 0,
    Ios = 1
}

/// <summary>Business event that triggered a notification.</summary>
public enum NotificationType
{
    OrderCreated = 0,
    OrderPaid = 1,
    OrderStatusChanged = 2,
    PaymentFailed = 3
}
