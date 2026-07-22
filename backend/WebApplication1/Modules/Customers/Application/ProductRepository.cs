using Microsoft.EntityFrameworkCore;
using WebApplication1.Common.Infrastructure;
using WebApplication1.Modules.Catalog.Application;

namespace WebApplication1.Modules.Customers.Infrastructure;

public class ProductRepository : IProductRepository
{
    private readonly StoreDbContext _storeDbContext;

    public ProductRepository(StoreDbContext storeDbContext)
    {
        _storeDbContext = storeDbContext;
    }

    
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _storeDbContext.Products
            .AsNoTracking()
            .Where(product => product.id == id)
            .Select(product => new ProductDto(
                product.id,
                product.Name,
                product.Price,
                product.ImageUrl))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _storeDbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(product => new ProductDto(
                    product.id, 
                    product.Name, 
                    product.Price, 
                    product.ImageUrl) )
            .ToListAsync(cancellationToken);
    }
}
