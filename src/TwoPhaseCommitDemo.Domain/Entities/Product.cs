namespace TwoPhaseCommitDemo.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public static Product Create(string sku, string name, decimal price, int stockQuantity)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            Price = price,
            StockQuantity = stockQuantity,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(string name, decimal price, int stockQuantity)
    {
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

