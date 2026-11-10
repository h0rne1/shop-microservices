using Microsoft.EntityFrameworkCore;

namespace PaymentService.Data;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";      // Approved | Declined
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.OrderId).HasColumnName("order_id");
            e.Property(p => p.Amount).HasColumnName("amount").HasPrecision(12, 2);
            e.Property(p => p.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(p => p.Reason).HasColumnName("reason").HasMaxLength(200);
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.HasIndex(p => p.OrderId);
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
                await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreatedAsync();
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
