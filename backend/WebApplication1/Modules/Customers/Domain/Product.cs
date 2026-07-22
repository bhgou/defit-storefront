namespace WebApplication1.Modules.Customers.Domain;

public class Product
{
    public Guid id { get; private set; }
    public string Name { get; private set; } 
    public decimal Price { get; private set; }
    public string ImageUrl { get; private set; }
    
    public Product(Guid id,string name, decimal price, string imageUrl)
    {
        if(string.IsNullOrEmpty(name))
            throw new ArgumentException("Название обязательно");
        if (price < 0)
            throw new ArgumentException("Цена не может быть отрицательной");
        id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name;
        Price = price;
        ImageUrl = imageUrl;
    }
}
