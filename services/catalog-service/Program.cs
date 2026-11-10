using CatalogService.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

var app = builder.Build();

await DbInitializer.InitAsync(app.Services, app.Logger);

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "catalog-service" }));

var products = app.MapGroup("/api/products");

// Список товаров
products.MapGet("/", async (CatalogDbContext db) =>
    await db.Products.OrderBy(p => p.Id).ToListAsync());

// Один товар
products.MapGet("/{id:int}", async (int id, CatalogDbContext db) =>
    await db.Products.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound(new { error = $"Product {id} not found" }));

// Добавить товар
products.MapPost("/", async (CreateProductRequest req, CatalogDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Price <= 0 || req.Stock < 0)
        return Results.BadRequest(new { error = "Name is required, price must be > 0, stock must be >= 0" });

    var p = new Product { Name = req.Name.Trim(), Price = req.Price, Stock = req.Stock };
    db.Products.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{p.Id}", p);
});

// Резерв товара (вызывается order-service). Атомарно: UPDATE ... WHERE stock >= qty
products.MapPost("/{id:int}/reserve", async (int id, QuantityRequest req, CatalogDbContext db) =>
{
    if (req.Quantity <= 0) return Results.BadRequest(new { error = "Quantity must be > 0" });

    var updated = await db.Products
        .Where(p => p.Id == id && p.Stock >= req.Quantity)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - req.Quantity));

    var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    if (product is null) return Results.NotFound(new { error = $"Product {id} not found" });
    if (updated == 0)
        return Results.Conflict(new { error = $"Not enough stock for product {id}: available {product.Stock}, requested {req.Quantity}" });

    return Results.Ok(new { product.Id, product.Name, product.Price, reserved = req.Quantity, stockLeft = product.Stock });
});

// Возврат товара на склад (компенсация при неудачной оплате)
products.MapPost("/{id:int}/release", async (int id, QuantityRequest req, CatalogDbContext db) =>
{
    if (req.Quantity <= 0) return Results.BadRequest(new { error = "Quantity must be > 0" });

    var updated = await db.Products
        .Where(p => p.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + req.Quantity));

    return updated == 0 ? Results.NotFound(new { error = $"Product {id} not found" }) : Results.Ok(new { id, released = req.Quantity });
});

app.Run();

record CreateProductRequest(string Name, decimal Price, int Stock);
record QuantityRequest(int Quantity);
