using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(p => p.Price).HasColumnName("price").HasPrecision(12, 2);
            e.Property(p => p.Stock).HasColumnName("stock");
        });
    }
}

public static class DbInitializer
{
    /// Создаёт таблицы (если их нет) и добавляет стартовые товары при пустой БД.
    /// Повторяет попытку, пока PostgreSQL не станет доступен.
    public static async Task InitAsync(IServiceProvider services, ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                await db.Database.EnsureCreatedAsync();
                if (!await db.Products.AnyAsync())
                {
                    db.Products.AddRange(
                        new Product { Name = "Ноутбук Aero 14", Price = 89990m, Stock = 5 },
                        new Product { Name = "Смартфон Nova X", Price = 39990m, Stock = 10 },
                        new Product { Name = "Наушники Pulse", Price = 7490m, Stock = 25 },
                        new Product { Name = "Монитор View 27", Price = 24990m, Stock = 7 });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded initial products");
                }
                return;
            }
            catch (Exception ex) when (attempt < 15)
            {
                logger.LogWarning("DB not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(2000);
            }
        }
    }
}
