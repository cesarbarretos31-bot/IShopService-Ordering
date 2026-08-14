namespace Ordering.API.Data;

public interface IOrderRepository
{
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken);
    Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken);
    Task<bool> UpdateStatusAsync(string id, OrderStatus expected, OrderStatus status, CancellationToken cancellationToken);
}
