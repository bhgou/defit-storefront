using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Account.Domain;
using WebApplication1.Modules.Notifications.Infrastructure;

namespace WebApplication1.Modules.Account.Endpoints;

public static class UserEndpoint
{
    public static void MapUser(this WebApplication app)
    {
        app.MapPost("/api/auth/telegram/start", async (
            StartTelegramRequest? request,
            StoreDbContext db,
            IOptions<TelegramBotOptions> options,
            CancellationToken ct) =>
        {
            var session = new TelegramLoginSession();
            db.TelegramLoginSessions.Add(session);
            await db.SaveChangesAsync(ct);

            var botName = string.IsNullOrWhiteSpace(options.Value.Username)
                ? "testtesttessfsdfsd_bot"
                : options.Value.Username.TrimStart('@');
            var referral = string.IsNullOrWhiteSpace(request?.ReferralCode)
                ? string.Empty
                : $"_{request.ReferralCode.Trim().ToUpperInvariant()}";

            return Results.Ok(new
            {
                sessionId = session.Id,
                expiresAt = session.ExpiresAt,
                telegramUrl = $"https://t.me/{botName}?start={session.Id:N}{referral}"
            });
        });

        app.MapGet("/api/auth/telegram/status/{sessionId:guid}", async (
            Guid sessionId,
            HttpContext http,
            StoreDbContext db,
            CancellationToken ct) =>
        {
            var session = await db.TelegramLoginSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct);
            if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
                return Results.NotFound(new { error = "Ссылка истекла. Создайте новую." });
            if (!session.IsCompleted)
                return Results.Ok(new { status = "pending", expiresAt = session.ExpiresAt });
            if (session.IsConsumed)
                return Results.BadRequest(new { error = "Сессия уже использована." });

            var user = await db.Users.SingleOrDefaultAsync(x => x.TelegramId == session.UserId, ct);
            if (user is null)
                return Results.NotFound(new { error = "Пользователь не найден." });

            session.IsConsumed = true;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim("telegram_id", user.TelegramId.ToString())
            };
            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { status = "completed", user = ToProfile(user) });
        });

        app.MapGet("/api/account/me", async (ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var user = await FindUser(principal, db, ct);
            if (user is null) return Results.Unauthorized();

            var referrals = await db.Users.CountAsync(x => x.ReferredByUserId == user.Id, ct);
            return Results.Ok(new
            {
                profile = ToProfile(user),
                referral = new
                {
                    code = user.ReferralCode,
                    link = $"http://localhost:5173/register?ref={user.ReferralCode}",
                    invited = referrals,
                    reward = referrals * 500
                }
            });
        }).RequireAuthorization();

        app.MapPatch("/api/account/me", async (
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            StoreDbContext db,
            CancellationToken ct) =>
        {
            var user = await FindUser(principal, db, ct);
            if (user is null) return Results.Unauthorized();
            if (!string.IsNullOrWhiteSpace(request.Name)) user.Name = request.Name.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToProfile(user));
        }).RequireAuthorization();

        app.MapPost("/api/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static async Task<User?> FindUser(
        ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
            : null;
    }

    private static object ToProfile(User user) => new
    {
        user.Id,
        user.Name,
        user.TelegramUsername,
        user.PhotoUrl,
        user.ReferralCode,
        user.CreatedAt
    };

    public sealed record StartTelegramRequest(string? ReferralCode);
    public sealed record UpdateProfileRequest(string Name);
}
