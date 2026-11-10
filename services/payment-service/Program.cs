using Microsoft.EntityFrameworkCore;
using PaymentService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

// Лимит одного платежа (имитация банковского правила). Настраивается через переменную окружения Payment__Limit
var limit = builder.Configuration.GetValue<decimal>("Payment:Limit", 100_000m);

var app = builder.Build();

await DbInitializer.InitAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "payment-service" }));

var payments = app.MapGroup("/api/payments");

// Провести оплату (вызывается order-service)
payments.MapPost("/", async (PaymentRequest req, PaymentDbContext db) =>
{
    if (req.OrderId <= 0 || req.Amount <= 0)
        return Results.BadRequest(new { error = "orderId and amount must be > 0" });

    var approved = req.Amount <= limit;
    var payment = new Payment
    {
        OrderId = req.OrderId,
        Amount = req.Amount,
        Status = approved ? "Approved" : "Declined",
        Reason = approved ? null : $"Amount exceeds limit {limit}",
        CreatedAt = DateTime.UtcNow
    };
    db.Payments.Add(payment);
    await db.SaveChangesAsync();
    return Results.Created($"/api/payments/{payment.Id}", payment);
});

// Список платежей, опционально по заказу: /api/payments?orderId=1
payments.MapGet("/", async (int? orderId, PaymentDbContext db) =>
    await db.Payments
        .Where(p => orderId == null || p.OrderId == orderId)
        .OrderBy(p => p.Id)
        .ToListAsync());

payments.MapGet("/{id:int}", async (int id, PaymentDbContext db) =>
    await db.Payments.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound(new { error = $"Payment {id} not found" }));

app.Run();

record PaymentRequest(int OrderId, decimal Amount);
