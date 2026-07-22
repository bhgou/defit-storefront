using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Catalog.Application;
using WebApplication1.Modules.Customers.Domain;
using WebApplication1.Modules.Customers.Infrastructure;

namespace WebApplication1.Modules.Customers.Endpoints;



public static class ProductEndpoints
{
    
    public static void MapProducts(this WebApplication app)
    {
        app.MapGet("/api/products/{id:guid}", async (
            Guid id,
            IProductRepository repository,
            CancellationToken cancellationToken) =>
        {
            var product = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return product is null
                ? Results.NotFound()
                : Results.Ok(product);
        });
        app.MapGet("/api/products", async (IProductRepository repository,CancellationToken cancellationToken) =>
        {
            var products = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(products);
        });
        app.MapPost("/api/product/",
            async (ProductDto request, StoreDbContext db, CancellationToken cancellationToken) =>
            {
                var product = new Product(
                    request.Id,
                    request.Name,
                    request.Price,
                    request.ImageUrl);
                
                db.Products.Add(product);
                
                await db.SaveChangesAsync(cancellationToken);
                
                var response = new ProductDto(
                    product.id,
                    product.Name,
                    product.Price,
                    product.ImageUrl);
                return Results.Created(
                    $"/api/products/{product.id}",
                    response);

            });

    }
}