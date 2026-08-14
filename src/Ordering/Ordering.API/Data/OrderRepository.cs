using MongoDB.Bson;
using MongoDB.Driver;

namespace Ordering.API.Data;

public class OrderRepository(IMongoDatabase database, IConfiguration configuration) : IOrderRepository
{
    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>(configuration["MongoDb:OrdersCollection"] ?? "orders");
    public async Task<Order> CreateAsync(Order order, CancellationToken ct) { await _orders.InsertOneAsync(order, cancellationToken: ct); return order; }
    public async Task<Order?> GetByIdAsync(string id, CancellationToken ct) => ObjectId.TryParse(id, out _) ? await _orders.Find(x => x.Id == id).FirstOrDefaultAsync(ct) : null;
    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(string customerId, CancellationToken ct) => await _orders.Find(x => x.CustomerId == customerId).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    public async Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => await _orders.Find(x => x.IdempotencyKey == key).FirstOrDefaultAsync(ct);
    public async Task<bool> UpdateStatusAsync(string id, OrderStatus expected, OrderStatus status, CancellationToken ct)
    {
        var result = await _orders.UpdateOneAsync(x => x.Id == id && x.Status == expected, Builders<Order>.Update.Set(x => x.Status, status), cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
}
