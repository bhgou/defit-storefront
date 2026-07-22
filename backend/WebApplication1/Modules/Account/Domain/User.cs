namespace WebApplication1.Modules.Account.Domain;

public class User
{
    public Guid Id { get; private set; }
    public long TelegramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TelegramUsername { get; set; }
    public string? PhotoUrl { get; set; }
    public string ReferralCode { get; private set; } = string.Empty;
    public Guid? ReferredByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private User() { }

    public User(long telegramId, string name, string? telegramUsername = null)
    {
        Id = Guid.NewGuid();
        TelegramId = telegramId;
        Name = name;
        TelegramUsername = telegramUsername;
        ReferralCode = $"DFT-{Id.ToString("N")[..7].ToUpperInvariant()}";
    }

    public void EnsureProfileData()
    {
        if (string.IsNullOrWhiteSpace(ReferralCode))
            ReferralCode = $"DFT-{Id.ToString("N")[..7].ToUpperInvariant()}";
        if (CreatedAt.Year < 2000)
            CreatedAt = DateTimeOffset.UtcNow;
    }

}
