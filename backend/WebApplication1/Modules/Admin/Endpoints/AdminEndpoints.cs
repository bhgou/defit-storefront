using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Catalog.Application;
using WebApplication1.Modules.Customers.Domain;
using Microsoft.AspNetCore.Hosting;

namespace WebApplication1.Modules.Admin.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").AddEndpointFilter(async (context, next) =>
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var ids = config.GetSection("Admin:TelegramIds").Get<long[]>() ?? [];
            var telegramId = long.TryParse(
                context.HttpContext.User.FindFirstValue("telegram_id"), out var value) ? value : 0;
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                return Results.Forbid();
            var hasAccess = ids.Contains(telegramId);
            if (!hasAccess && ids.Length == 0)
                hasAccess = true;
            if (!hasAccess) return Results.Forbid();
            return await next(context);
        });

        admin.MapGet("/products", async (StoreDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Products.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new ProductDto(x.id, x.Name, x.Price, x.ImageUrl)).ToListAsync(ct)));

        admin.MapPost("/products", async (CreateProductRequest request, StoreDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
                return Results.BadRequest(new { error = "Проверьте название и цену." });
            var product = new Product(Guid.NewGuid(), request.Name.Trim(), request.Price, request.ImageUrl?.Trim() ?? string.Empty);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{product.id}", new ProductDto(product.id, product.Name, product.Price, product.ImageUrl));
        });

        admin.MapPost("/uploads", async (IFormFile file, IWebHostEnvironment environment, HttpRequest request, CancellationToken ct) =>
        {
            if (file.Length == 0 || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Загрузите файл изображения." });
            if (file.Length > 8 * 1024 * 1024)
                return Results.BadRequest(new { error = "Изображение не должно превышать 8 МБ." });
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowed.Contains(extension)) return Results.BadRequest(new { error = "Поддерживаются JPG, PNG, WEBP и GIF." });
            var folder = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
            Directory.CreateDirectory(folder);
            var filename = $"{Guid.NewGuid():N}{extension}";
            await using var stream = File.Create(Path.Combine(folder, filename));
            await file.CopyToAsync(stream, ct);
            return Results.Ok(new { url = $"{request.Scheme}://{request.Host}/uploads/{filename}" });
        }).DisableAntiforgery();

        admin.MapGet("/orders", async (StoreDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Orders.AsNoTracking().Include(x => x.Items).OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.Number, x.Status, x.Total, x.CustomerName, x.Phone, x.DeliveryAddress, x.TrackingNumber, x.CreatedAt, Items = x.Items.Count })
                .ToListAsync(ct)));

        admin.MapPatch("/orders/{id:guid}/status", async (Guid id, UpdateStatusRequest request, StoreDbContext db, CancellationToken ct) =>
        {
            var allowed = new[] { "created", "paid", "packing", "shipped", "delivered", "cancelled" };
            if (!allowed.Contains(request.Status)) return Results.BadRequest(new { error = "Неизвестный статус." });
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (order is null) return Results.NotFound();
            order.Status = request.Status;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { order.Id, order.Status });
        });
    }

    public sealed record CreateProductRequest(string Name, decimal Price, string? ImageUrl);
    public sealed record UpdateStatusRequest(string Status);
}
