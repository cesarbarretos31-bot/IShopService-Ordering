namespace Ordering.API.Orders.GetOrders;

public record GetOrdersQuery : IQuery<IReadOnlyList<Order>>;

public class GetOrdersQueryHandler(IOrderRepository repository)
    : IQueryHandler<GetOrdersQuery, IReadOnlyList<Order>>
{
    public Task<IReadOnlyList<Order>> Handle(
        GetOrdersQuery query,
        CancellationToken ct) => repository.GetAllAsync(ct);
}

public class GetOrdersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/orders",
                async (ISender sender) =>
                    Results.Ok(await sender.Send(new GetOrdersQuery())))
            .WithName("GetOrders")
            .WithSummary("Consultar todas las órdenes")
            .WithDescription("Devuelve todas las órdenes ordenadas por fecha descendente.")
            .Produces<IReadOnlyList<Order>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
