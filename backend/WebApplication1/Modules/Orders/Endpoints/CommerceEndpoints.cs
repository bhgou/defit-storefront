using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Orders.Domain;

namespace WebApplication1.Modules.Orders.Endpoints;

public static class CommerceEndpoints
{
    public static void MapCommerce(this WebApplication app)
    {
        var cart = app.MapGroup("/api/cart").RequireAuthorization();

        cart.MapGet("/", async (ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
            Results.Ok(await GetCart(UserId(principal), db, ct)));

        cart.MapPost("/items", async (CartItemRequest request, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            if (request.Quantity is < 1 or > 10) return Results.BadRequest(new { error = "Количество: от 1 до 10." });
            var product = await db.Products.SingleOrDefaultAsync(x => x.id == request.ProductId, ct);
            if (product is null) return Results.NotFound();
            var userId = UserId(principal);
            var size = request.Size.Trim().ToUpperInvariant();
            var item = await db.CartItems.SingleOrDefaultAsync(
                x => x.UserId == userId && x.ProductId == request.ProductId && x.Size == size, ct);
            if (item is null)
                db.CartItems.Add(new CartItem(userId, request.ProductId, request.Quantity, size, product.Name, product.Price, product.ImageUrl));
            else
            {
                item.Quantity = Math.Min(10, item.Quantity + request.Quantity);
                item.ProductName = product.Name;
                item.UnitPrice = product.Price;
                item.ImageUrl = product.ImageUrl;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetCart(userId, db, ct));
        });

        cart.MapPatch("/items/{id:guid}", async (Guid id, UpdateCartItemRequest request, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var item = await db.CartItems.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId(principal), ct);
            if (item is null) return Results.NotFound();
            if (request.Quantity <= 0) db.CartItems.Remove(item);
            else item.Quantity = Math.Min(10, request.Quantity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetCart(UserId(principal), db, ct));
        });

        cart.MapDelete("/items/{id:guid}", async (Guid id, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var item = await db.CartItems.SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId(principal), ct);
            if (item is null) return Results.NotFound();
            db.CartItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await GetCart(UserId(principal), db, ct));
        });

        app.MapPost("/api/promocodes/validate", async (PromoRequest request, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var promo = await ValidPromo(request.Code, db, ct);
            return promo is null
                ? Results.NotFound(new { error = "Промокод не найден или больше не действует." })
                : Results.Ok(new { code = promo.Code, discountPercent = promo.DiscountPercent });
        }).RequireAuthorization();

        var orders = app.MapGroup("/api/orders").RequireAuthorization();
        orders.MapGet("/", async (ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var result = await db.Orders.AsNoTracking().Include(x => x.Items)
                .Where(x => x.UserId == UserId(principal)).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(result.Select(ToOrder));
        });
        orders.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId(principal), ct);
            return order is null ? Results.NotFound() : Results.Ok(ToOrder(order));
        });
        orders.MapPost("/{id:guid}/pay", async (Guid id, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId(principal), ct);
            if (order is null) return Results.NotFound();
            if (order.Status == "cancelled") return Results.BadRequest(new { error = "Отменённый заказ нельзя оплатить." });
            if (order.Status == "created")
            {
                order.Status = "paid";
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(ToOrder(order));
        });
        orders.MapPost("/", async (CheckoutRequest request, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var userId = UserId(principal);
            var items = await db.CartItems.Where(x => x.UserId == userId).ToListAsync(ct);
            if (items.Count == 0) return Results.BadRequest(new { error = "Корзина пуста." });
            if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.DeliveryAddress))
                return Results.BadRequest(new { error = "Заполните ФИО, телефон и адрес доставки." });
            var productIds = items.Select(x => x.ProductId).ToArray();
            var products = await db.Products.Where(x => productIds.Contains(x.id)).ToDictionaryAsync(x => x.id, ct);
            foreach (var item in items)
            {
                if (products.TryGetValue(item.ProductId, out var currentProduct))
                {
                    item.ProductName = currentProduct.Name;
                    item.UnitPrice = currentProduct.Price;
                    item.ImageUrl = currentProduct.ImageUrl;
                }
            }
            var unavailableItems = items.Where(x => !products.ContainsKey(x.ProductId) && string.IsNullOrWhiteSpace(x.ProductName)).ToList();
            if (unavailableItems.Count > 0)
            {
                db.CartItems.RemoveRange(unavailableItems);
                items = items.Except(unavailableItems).ToList();
                await db.SaveChangesAsync(ct);
                if (items.Count == 0)
                    return Results.BadRequest(new { error = "Товары из корзины больше недоступны. Корзина очищена." });
            }
            var subtotal = items.Sum(x => x.UnitPrice * x.Quantity);
            var promo = await ValidPromo(request.PromoCode, db, ct);
            var discount = promo is null ? 0 : Math.Round(subtotal * promo.DiscountPercent / 100m, 2);
            var order = new Order(userId, subtotal, discount, request.CustomerName.Trim(), request.Phone.Trim(), request.DeliveryAddress.Trim(), promo?.Code);
            foreach (var item in items)
                order.Items.Add(new OrderItem(item.ProductId, item.ProductName, item.ImageUrl, item.UnitPrice, item.Quantity, item.Size));
            if (promo is not null) promo.Uses++;
            db.Orders.Add(order);
            db.CartItems.RemoveRange(items);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", ToOrder(order));
        });
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    private static async Task<object> GetCart(Guid userId, StoreDbContext db, CancellationToken ct)
    {
        var items = await (from item in db.CartItems.AsNoTracking()
            join product in db.Products.AsNoTracking() on item.ProductId equals product.id into products
            from product in products.DefaultIfEmpty()
            where item.UserId == userId
            orderby item.AddedAt
            select new
            {
                item.Id, item.ProductId,
                Name = item.ProductName != "" ? item.ProductName : product != null ? product.Name : "Недоступный товар",
                ImageUrl = item.ImageUrl != "" ? item.ImageUrl : product != null ? product.ImageUrl : "",
                Price = item.UnitPrice > 0 ? item.UnitPrice : product != null ? product.Price : 0,
                item.Quantity, item.Size
            })
            .ToListAsync(ct);
        return new { items, count = items.Sum(x => x.Quantity), subtotal = items.Sum(x => x.Price * x.Quantity) };
    }

    private static async Task<PromoCode?> ValidPromo(string? code, StoreDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim().ToUpperInvariant();
        return await db.PromoCodes.SingleOrDefaultAsync(x => x.Code == normalized && x.IsActive &&
            (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow) && x.Uses < x.MaxUses, ct);
    }

    private static object ToOrder(Order order) => new
    {
        order.Id, order.Number, order.Subtotal, order.Discount, order.Total, order.Status,
        order.CustomerName, order.Phone, order.DeliveryAddress, order.PromoCode, order.TrackingNumber, order.CreatedAt, order.EstimatedDelivery,
        order.Items,
        timeline = Timeline(order)
    };

    private static object[] Timeline(Order order)
    {
        var statuses = new[] { ("created", "Заказ создан"), ("paid", "Оплата подтверждена"), ("packing", "Собираем заказ"), ("shipped", "Передан в доставку"), ("delivered", "Доставлен") };
        var current = Array.FindIndex(statuses, x => x.Item1 == order.Status);
        if (current < 0) current = 0;
        return statuses.Select((x, i) => (object)new { key = x.Item1, label = x.Item2, completed = i <= current, active = i == current }).ToArray();
    }

    public sealed record CartItemRequest(Guid ProductId, int Quantity, string Size);
    public sealed record UpdateCartItemRequest(int Quantity);
    public sealed record PromoRequest(string Code);
    public sealed record CheckoutRequest(string CustomerName, string Phone, string DeliveryAddress, string? PromoCode);
}
