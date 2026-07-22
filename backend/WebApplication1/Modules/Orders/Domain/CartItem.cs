namespace WebApplication1.Modules.Orders.Domain;

public sealed class CartItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; set; }
    public string Size { get; set; } = "M";
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; private set; } = DateTimeOffset.UtcNow;

    private CartItem() { }

    public CartItem(Guid userId, Guid productId, int quantity, string size, string productName, decimal unitPrice, string imageUrl)
    {
        UserId = userId;
        ProductId = productId;
        Quantity = quantity;
        Size = size;
        ProductName = productName;
        UnitPrice = unitPrice;
        ImageUrl = imageUrl;
    }
}
