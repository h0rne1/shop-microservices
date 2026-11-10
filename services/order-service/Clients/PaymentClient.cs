namespace OrderService.Clients;

public record PaymentResult(int Id, int OrderId, decimal Amount, string Status, string? Reason);

/// HTTP-клиент к payment-service (адрес задаётся переменной Services__PaymentUrl)
public class PaymentClient(HttpClient http)
{
    public async Task<PaymentResult> PayAsync(int orderId, decimal amount, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("/api/payments", new { orderId, amount }, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PaymentResult>(ct))!;
    }
}
