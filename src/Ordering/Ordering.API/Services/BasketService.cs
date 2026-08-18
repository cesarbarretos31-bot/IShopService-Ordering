using System.Net;
using System.Net.Http.Json;

namespace Ordering.API.Services;

public interface IBasketService
{
    Task<BasketDto> GetAsync(
        string userName,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string userName,
        CancellationToken cancellationToken);
}

public sealed record BasketEnvelope(BasketDto Cart);

public sealed record BasketDto(
    string UserName,
    List<BasketItemDto>? Items);

public sealed record BasketItemDto(
    int Quantity,
    decimal Price,
    Guid ProductId,
    string ProductName);

public class BasketService(HttpClient client) : IBasketService
{
    public async Task<BasketDto> GetAsync(
        string userName,
        CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"/basket/{Uri.EscapeDataString(userName)}",
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new BadRequestException(
                "El basket indicado no existe.");
        }

        response.EnsureSuccessStatusCode();

        var envelope =
            await response.Content.ReadFromJsonAsync<BasketEnvelope>(
                cancellationToken: ct);

        return envelope?.Cart
            ?? throw new BadRequestException(
                "La respuesta de Basket.API no es válida.");
    }

    public async Task DeleteAsync(
        string userName,
        CancellationToken ct)
    {
        using var response = await client.DeleteAsync(
            $"/basket/{Uri.EscapeDataString(userName)}",
            ct);

        // Si ya no existe, consideramos cumplida
        // la eliminación.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}