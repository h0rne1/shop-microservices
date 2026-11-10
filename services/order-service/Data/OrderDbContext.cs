using Microsoft.EntityFrameworkCore;

namespace OrderService.Data;

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = "";          // Pending | Paid | PaymentFailed | Rejected
    public decimal Total { get; set; }
    public int? PaymentId { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Property(o => o.Id).HasColumnName("id");
            e.Property(o => o.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
            e.Property(o => o.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(o => o.Total).HasColumnName("total").HasPrecision(12, 2);
            e.Property(o => o.PaymentId).HasColumnName("payment_id");
            e.Property(o => o.Comment).HasColumnName("comment").HasMaxLength(500);
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.OrderId).HasColumnName("order_id");
            e.Property(i => i.ProductId).HasColumnName("product_id");
            e.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(200);
            e.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
            e.Property(i => i.Quantity).HasColumnName("quantity");
        });
    }
}

public static class DbInitializer
{
    public static async Task InitAsync(IServiceProvider services, ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.EnsureCreatedAsync();
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
