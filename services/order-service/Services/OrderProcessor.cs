using OrderService.Clients;
using OrderService.Data;

namespace OrderService.Services;

public record OrderItemRequest(int ProductId, int Quantity);
public record CreateOrderRequest(string CustomerName, List<OrderItemRequest> Items);

/// Бизнес-логика оформления заказа:
/// 1) резерв товаров в catalog-service;
/// 2) оплата в payment-service;
/// 3) при отказе в оплате — возврат товаров на склад (компенсация).
public class OrderProcessor(OrderDbContext db, CatalogClient catalog, PaymentClient payments, ILogger<OrderProcessor> log)
{
    public async Task<Order> PlaceOrderAsync(CreateOrderRequest req, CancellationToken ct = default)
    {
        var order = new Order { CustomerName = req.CustomerName.Trim(), Status = "Pending", CreatedAt = DateTime.UtcNow };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // 1. Резерв товаров
        var reserved = new List<(int ProductId, int Qty)>();
        try
        {
            foreach (var item in req.Items)
            {
                var p = await catalog.ReserveAsync(item.ProductId, item.Quantity, ct);
                reserved.Add((p.Id, item.Quantity));
                order.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = item.Quantity });
            }
        }
        catch (ReservationException ex)
        {
            await ReleaseAllAsync(reserved, ct);
            order.Items.Clear();
            order.Status = "Rejected";
            order.Comment = ex.Message;
            await db.SaveChangesAsync(ct);
            log.LogInformation("Order {Id} rejected: {Reason}", order.Id, ex.Message);
            return order;
        }

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        // 2. Оплата
        var payment = await payments.PayAsync(order.Id, order.Total, ct);
        order.PaymentId = payment.Id;

        if (payment.Status == "Approved")
        {
            order.Status = "Paid";
        }
        else
        {
            // 3. Компенсация: вернуть товар на склад
            await ReleaseAllAsync(reserved, ct);
            order.Status = "PaymentFailed";
            order.Comment = payment.Reason;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Order {Id} -> {Status}, total {Total}", order.Id, order.Status, order.Total);
        return order;
    }

    private async Task ReleaseAllAsync(IEnumerable<(int ProductId, int Qty)> items, CancellationToken ct)
    {
        foreach (var (productId, qty) in items)
            await catalog.ReleaseAsync(productId, qty, ct);
    }
}
