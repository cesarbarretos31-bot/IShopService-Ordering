using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ordering.API.Models;

[BsonIgnoreExtraElements]
public class Order
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string CustomerId { get; set; } = default!;
    public string BasketId { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public OrderStatus Status { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Subtotal { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Tax { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Total { get; set; }

    public string IdempotencyKey { get; set; } = default!;
}

public class OrderItem
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}