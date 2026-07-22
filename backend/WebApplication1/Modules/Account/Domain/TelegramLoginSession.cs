namespace WebApplication1.Modules.Account.Domain;

public class TelegramLoginSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ExpiresAt { get; set; }
        = DateTimeOffset.UtcNow.AddMinutes(5);
    public long UserId { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsConsumed { get; set; }
}
