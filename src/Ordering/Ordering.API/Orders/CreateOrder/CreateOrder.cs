using FluentValidation;
using MongoDB.Driver;
using Ordering.API.Services;

namespace Ordering.API.Orders.CreateOrder;

public record CreateOrderCommand(string CustomerId, string BasketId, string IdempotencyKey) : ICommand<CreateOrderResult>;
public record CreateOrderResult(Order Order, bool Created);

public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BasketId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x).Must(x => string.Equals(x.CustomerId, x.BasketId, StringComparison.Ordinal)).WithMessage("CustomerId debe coincidir con BasketId.");
    }
}

public class CreateOrderCommandHandler(IOrderRepository repository, IBasketService baskets, IConfiguration configuration) : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var existing = await repository.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        if (existing is not null) return new(existing, false);
        var basket = await baskets.GetAsync(command.BasketId, ct);
        if (!string.Equals(basket.UserName, command.BasketId, StringComparison.Ordinal)) throw new BadRequestException("El basket no pertenece al cliente indicado.");
        if (basket.Items is null || basket.Items.Count == 0) throw new BadRequestException("No se puede crear una orden desde un basket vacío.");
        if (basket.Items.Any(x => x.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(x.ProductName) || x.Quantity <= 0 || x.Price < 0)) throw new BadRequestException("El basket contiene productos inválidos.");
        var items = basket.Items.Select(x => new OrderItem { ProductId = x.ProductId, ProductName = x.ProductName, Quantity = x.Quantity, UnitPrice = x.Price, LineTotal = x.Price * x.Quantity }).ToList();
        var subtotal = items.Sum(x => x.LineTotal);
        var rate = configuration.GetValue<decimal>("Ordering:TaxRate");
        var tax = decimal.Round(subtotal * rate, 2, MidpointRounding.AwayFromZero);
        var order = new Order { CustomerId = command.CustomerId, BasketId = command.BasketId, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Pending, Items = items, Subtotal = subtotal, Tax = tax, Total = subtotal + tax, IdempotencyKey = command.IdempotencyKey };
        try { return new(await repository.CreateAsync(order, ct), true); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await repository.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct);
            if (existing is not null) return new(existing, false);
            throw;
        }
    }
}

public record CreateOrderRequest(string CustomerId, string BasketId);
public class CreateOrderEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) => app.MapPost("/api/orders", async (CreateOrderRequest request, HttpRequest http, ISender sender) =>
    {
        var key = http.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
        var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.BasketId, key));
        return result.Created ? Results.Created($"/api/orders/{result.Order.Id}", result.Order) : Results.Ok(result.Order);
    }).WithName("CreateOrder").WithSummary("Crear una orden").WithDescription("Crea una orden Pending desde el basket del mismo usuario. Idempotency-Key es obligatorio.")
      .Produces<Order>(201).Produces<Order>(200).ProducesProblem(400).ProducesProblem(500);
}
