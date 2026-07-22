namespace WebApplication1.Modules.Orders.Domain;

public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public decimal Subtotal { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Total { get; private set; }
    public string Status { get; set; } = "created";
    public string DeliveryAddress { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? PromoCode { get; private set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EstimatedDelivery { get; set; } = DateTimeOffset.UtcNow.AddDays(5);
    public List<OrderItem> Items { get; private set; } = [];

    private Order() { }

    public Order(Guid userId, decimal subtotal, decimal discount, string customerName, string phone, string address, string? promoCode)
    {
        UserId = userId;
        Subtotal = subtotal;
        Discount = discount;
        Total = Math.Max(0, subtotal - discount);
        DeliveryAddress = address;
        CustomerName = customerName;
        Phone = phone;
        PromoCode = promoCode;
        Number = $"DF-{DateTime.UtcNow:yyMMdd}-{Id.ToString("N")[..5].ToUpperInvariant()}";
        TrackingNumber = $"RU{Random.Shared.Next(100000000, 999999999)}DF";
    }
}

public sealed class OrderItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public string Size { get; private set; } = string.Empty;

    private OrderItem() { }

    public OrderItem(Guid productId, string name, string imageUrl, decimal price, int quantity, string size)
    {
        ProductId = productId;
        ProductName = name;
        ImageUrl = imageUrl;
        UnitPrice = price;
        Quantity = quantity;
        Size = size;
    }
}
