using FluentValidation;
using Ordering.API.Exceptions;

namespace Ordering.API.Orders.GetOrder;
public record GetOrderQuery(string Id) : IQuery<Order>;
public class GetOrderHandler(IOrderRepository repository) : IQueryHandler<GetOrderQuery, Order>
{ public async Task<Order> Handle(GetOrderQuery q, CancellationToken ct) => await repository.GetByIdAsync(q.Id, ct) ?? throw new OrderNotFoundException(q.Id); }
public class GetOrderEndpoint : ICarterModule
{ public void AddRoutes(IEndpointRouteBuilder app) => app.MapGet("/api/orders/{id}", async (string id, ISender sender) => Results.Ok(await sender.Send(new GetOrderQuery(id)))).WithName("GetOrder").WithSummary("Consultar orden por id").Produces<Order>(200).ProducesProblem(404).ProducesProblem(500); }
