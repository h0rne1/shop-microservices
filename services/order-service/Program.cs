using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Data;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddDbContext<OrderDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("OrderDb")));

builder.Services.AddHttpClient<CatalogClient>(c =>
{
    c.BaseAddress = new Uri(cfg["Services:CatalogUrl"] ?? "http://catalog-service:8080");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<PaymentClient>(c =>
{
    c.BaseAddress = new Uri(cfg["Services:PaymentUrl"] ?? "http://payment-service:8080");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<OrderProcessor>();

var app = builder.Build();

await DbInitializer.InitAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "order-service" }));

var orders = app.MapGroup("/api/orders");

// Оформить заказ
orders.MapPost("/", async (CreateOrderRequest req, OrderProcessor processor, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.CustomerName) || req.Items is null || req.Items.Count == 0 || req.Items.Any(i => i.Quantity <= 0))
        return Results.BadRequest(new { error = "customerName and at least one item with quantity > 0 are required" });

    try
    {
        var order = await processor.PlaceOrderAsync(req, ct);
        return order.Status == "Rejected"
            ? Results.UnprocessableEntity(order)
            : Results.Created($"/api/orders/{order.Id}", order);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem($"Dependent service unavailable: {ex.Message}", statusCode: 503);
    }
});

// Список заказов
orders.MapGet("/", async (OrderDbContext db) =>
    await db.Orders.Include(o => o.Items).OrderBy(o => o.Id).ToListAsync());

// Заказ по id
orders.MapGet("/{id:int}", async (int id, OrderDbContext db) =>
    await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id) is { } o
        ? Results.Ok(o)
        : Results.NotFound(new { error = $"Order {id} not found" }));

app.Run();
