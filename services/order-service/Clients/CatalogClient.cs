using System.Net;

namespace OrderService.Clients;

public record ReservedProduct(int Id, string Name, decimal Price, int Reserved, int StockLeft);

public class ReservationException(string message) : Exception(message);

/// HTTP-клиент к catalog-service (адрес задаётся переменной Services__CatalogUrl)
public class CatalogClient(HttpClient http)
{
    public async Task<ReservedProduct> ReserveAsync(int productId, int quantity, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"/api/products/{productId}/reserve", new { quantity }, ct);
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>(ct);
            throw new ReservationException(err?.Error ?? $"Cannot reserve product {productId}");
        }
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReservedProduct>(ct))!;
    }

    public async Task ReleaseAsync(int productId, int quantity, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync($"/api/products/{productId}/release", new { quantity }, ct);
        resp.EnsureSuccessStatusCode();
    }

    private record ErrorBody(string Error);
}
