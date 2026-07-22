namespace WebApplication1.Modules.Orders.Domain;

public sealed class PromoCode
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public int DiscountPercent { get; private set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int Uses { get; set; }

    private PromoCode() { }

    public PromoCode(string code, int discountPercent, int maxUses = 1000)
    {
        Code = code.ToUpperInvariant();
        DiscountPercent = discountPercent;
        MaxUses = maxUses;
    }
}
