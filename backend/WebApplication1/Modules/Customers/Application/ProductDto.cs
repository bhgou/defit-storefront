namespace WebApplication1.Modules.Catalog.Application;

public sealed record ProductDto(
    Guid Id,
    string Name,
    decimal Price,
    string ImageUrl);

public interface IProductRepository
{
    Task<ProductDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
    
    Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken);
}
