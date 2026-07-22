using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Account.Endpoints;
using WebApplication1.Modules.Admin.Endpoints;
using WebApplication1.Modules.Catalog.Application;
using WebApplication1.Modules.Customers.Endpoints;
using WebApplication1.Modules.Customers.Infrastructure;
using WebApplication1.Modules.Notifications.Infrastructure;
using WebApplication1.Modules.Orders.Domain;
using WebApplication1.Modules.Orders.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException(
        "Connection string 'Database' is not configured.");

builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.Configure<TelegramBotOptions>(
    builder.Configuration.GetSection(TelegramBotOptions.SectionName));
builder.Services.AddHostedService<TelegramBotBackgroundService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "defit.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
var app = builder.Build();
app.UseStaticFiles();
app.UseCors("Frontend");
app.MapProducts();
app.UseAuthentication();
app.UseAuthorization();
app.MapUser();
app.MapCommerce();
app.MapAdmin();
app.UseHttpsRedirection();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    await db.Database.MigrateAsync();
    var legacyUsers = await db.Users
        .Where(user => user.ReferralCode == "" || user.CreatedAt.Year < 2000)
        .ToListAsync();
    foreach (var user in legacyUsers) user.EnsureProfileData();
    if (!await db.PromoCodes.AnyAsync())
    {
        db.PromoCodes.AddRange(
            new PromoCode("FIRST10", 10),
            new PromoCode("DEFIT15", 15, 250));
    }
    await db.SaveChangesAsync();
}

app.Run();
