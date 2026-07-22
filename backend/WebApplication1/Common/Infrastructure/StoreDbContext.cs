using Microsoft.EntityFrameworkCore;
using WebApplication1.Modules.Account.Domain;
using WebApplication1.Modules.Customers.Domain;
using WebApplication1.Modules.Orders.Domain;

namespace WebApplication1.Common.Infrastructure;

public sealed class StoreDbContext(
    DbContextOptions<StoreDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TelegramLoginSession> TelegramLoginSessions => Set<TelegramLoginSession>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelegramLoginSession>(entity =>
        {
            entity.ToTable("Keys");
            entity.Property(session => session.Id).HasColumnName("KeyId");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.TelegramId).IsUnique();
            entity.HasIndex(user => user.ReferralCode).IsUnique();
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(item => new { item.UserId, item.ProductId, item.Size }).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(order => order.Number).IsUnique();
            entity.OwnsMany(order => order.Items, items =>
            {
                items.ToTable("OrderItems");
                items.WithOwner().HasForeignKey("OrderId");
            });
        });

        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasIndex(promo => promo.Code).IsUnique();
        });
    }
}
