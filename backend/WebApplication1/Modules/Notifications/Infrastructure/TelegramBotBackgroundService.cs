using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WebApplication1.Common.Infrastructure;
using AccountUser = WebApplication1.Modules.Account.Domain.User;

namespace WebApplication1.Modules.Notifications.Infrastructure;

public sealed class TelegramBotBackgroundService(
    IOptions<TelegramBotOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<TelegramBotBackgroundService> logger)
    : BackgroundService
{
    private readonly TelegramBotOptions _options = options.Value;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            logger.LogWarning(
                "Telegram bot is disabled. Set the TelegramBot__Token environment variable to enable it.");
            return;
        }

        var botClient = new TelegramBotClient(_options.Token);

        try
        {
            var bot = await botClient.GetMe(stoppingToken);

            await botClient.SetMyCommands(
                [
                    new BotCommand { Command = "start", Description = "Начать работу с ботом" },
                    new BotCommand { Command = "help", Description = "Показать доступные команды" }
                ],
                cancellationToken: stoppingToken);

            logger.LogInformation("Telegram bot @{BotUsername} started", bot.Username);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message]
            };

            await botClient.ReceiveAsync(
                HandleUpdateAsync,
                HandlePollingErrorAsync,
                receiverOptions,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Telegram bot could not be started");
        }
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message?.Text is null || message.From is null)
        {
            return;
        }

        try
        {
            var command = message.Text.Split(' ', 2)[0].Split('@', 2)[0].ToLowerInvariant();
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            
            switch (command)
            {
                case "/start":
                    var commandParts = message.Text.Split(
                        ' ',
                        2,
                        StringSplitOptions.RemoveEmptyEntries);

                    if (commandParts.Length < 2)
                    {
                        await botClient.SendMessage(
                            message.Chat.Id,
                            "Откройте страницу регистрации на сайте и нажмите «Открыть Telegram».",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var payload = commandParts[1].Split('_', 2);
                    if (!Guid.TryParse(payload[0], out var sessionId))
                    {
                        // Неправильный идентификатор сессии
                        break;
                    }

                    var loginSession = await db.TelegramLoginSessions.SingleOrDefaultAsync(x =>
                        x.Id == sessionId && !x.IsCompleted && x.ExpiresAt > DateTimeOffset.UtcNow,
                        cancellationToken);
                    if (loginSession is null)
                    {
                        await botClient.SendMessage(
                            message.Chat.Id,
                            "Ссылка недействительна или устарела.",
                            cancellationToken: cancellationToken);
                        return;
                    }
                    var user = await RegisterUserAsync(message.From, cancellationToken);
                    if (payload.Length == 2 && user.ReferredByUserId is null)
                    {
                        var referralCode = payload[1].Trim().ToUpperInvariant();
                        var referrer = await db.Users.SingleOrDefaultAsync(
                            x => x.ReferralCode == referralCode && x.Id != user.Id,
                            cancellationToken);
                        if (referrer is not null)
                        {
                            db.Users.Attach(user);
                            user.ReferredByUserId = referrer.Id;
                        }
                    }
                    loginSession.IsCompleted = true;
                    loginSession.UserId = message.From.Id;
                    await db.SaveChangesAsync(cancellationToken);
                    await botClient.SendMessage(
                        message.Chat.Id,
                        $"Привет, {message.From.FirstName}! Вы подключили Telegram-бот магазина.\n\n" +
                        "Используйте /help, чтобы посмотреть доступные команды.",
                        cancellationToken: cancellationToken);
                    break;

                case "/help":
                    await botClient.SendMessage(
                        message.Chat.Id,
                        "Доступные команды:\n/start — зарегистрироваться в боте\n/help — показать эту справку",
                        cancellationToken: cancellationToken);
                    break;

                default:
                    await botClient.SendMessage(
                        message.Chat.Id,
                        "Неизвестная команда. Используйте /help.",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to handle Telegram update {UpdateId}", update.Id);
        }
    }

    private async Task<AccountUser> RegisterUserAsync(
        Telegram.Bot.Types.User telegramUser,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(
            item => item.TelegramId == telegramUser.Id,
            cancellationToken);

        var name = string.Join(
            ' ',
            new[] { telegramUser.FirstName, telegramUser.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (user is null)
        {
            user = new AccountUser(telegramUser.Id, name, telegramUser.Username);
            db.Users.Add(user);
        }
        else
        {
            user.Name = name;
            user.TelegramUsername = telegramUser.Username;
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private Task HandlePollingErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}
