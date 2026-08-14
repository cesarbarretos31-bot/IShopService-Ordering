namespace Ordering.API.Orders.GetOrdersByCustomer;
public record GetOrdersByCustomerQuery(string CustomerId) : IQuery<IReadOnlyList<Order>>;
public class GetOrdersByCustomerHandler(IOrderRepository repository) : IQueryHandler<GetOrdersByCustomerQuery, IReadOnlyList<Order>>
{ public Task<IReadOnlyList<Order>> Handle(GetOrdersByCustomerQuery q, CancellationToken ct) => string.IsNullOrWhiteSpace(q.CustomerId) ? throw new BadRequestException("CustomerId es obligatorio.") : repository.GetByCustomerIdAsync(q.CustomerId, ct); }
public class GetOrdersByCustomerEndpoint : ICarterModule
{ public void AddRoutes(IEndpointRouteBuilder app) => app.MapGet("/api/orders/customer/{customerId}", async (string customerId, ISender sender) => Results.Ok(await sender.Send(new GetOrdersByCustomerQuery(customerId)))).WithName("GetOrdersByCustomer").WithSummary("Consultar órdenes por cliente").WithDescription("Solo devuelve órdenes cuyo CustomerId coincide exactamente.").Produces<IReadOnlyList<Order>>(200).ProducesProblem(400).ProducesProblem(500); }
