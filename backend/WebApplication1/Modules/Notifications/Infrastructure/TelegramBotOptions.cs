namespace WebApplication1.Modules.Notifications.Infrastructure;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string Token { get; init; } = string.Empty;
    public string Username { get; init; } = "testtesttessfsdfsd_bot";
}
