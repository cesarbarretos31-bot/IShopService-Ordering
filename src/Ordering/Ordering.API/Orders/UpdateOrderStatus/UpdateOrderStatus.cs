using FluentValidation;
using Ordering.API.Exceptions;

namespace Ordering.API.Orders.UpdateOrderStatus;
public record UpdateOrderStatusCommand(string Id, string Status) : ICommand<Order>;
public class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusCommand>
{ public UpdateOrderStatusValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Status).Must(x => Enum.TryParse<OrderStatus>(x, true, out _)).WithMessage("Status debe ser Confirmed o Cancelled."); } }
public class UpdateOrderStatusHandler(IOrderRepository repository) : ICommandHandler<UpdateOrderStatusCommand, Order>
{
    public async Task<Order> Handle(UpdateOrderStatusCommand command, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(command.Id, ct) ?? throw new OrderNotFoundException(command.Id);
        var target = Enum.Parse<OrderStatus>(command.Status, true);
        if (order.Status != OrderStatus.Pending || target is not (OrderStatus.Confirmed or OrderStatus.Cancelled)) throw new OrderConflictException($"La transición {order.Status} -> {target} no está permitida.");
        if (!await repository.UpdateStatusAsync(order.Id, OrderStatus.Pending, target, ct)) throw new OrderConflictException("La orden fue modificada concurrentemente.");
        order.Status = target;
        return order;
    }
}
public record UpdateOrderStatusRequest(string Status);
public class UpdateOrderStatusEndpoint : ICarterModule
{ public void AddRoutes(IEndpointRouteBuilder app) => app.MapPatch("/api/orders/{id}/status", async (string id, UpdateOrderStatusRequest request, ISender sender) => Results.Ok(await sender.Send(new UpdateOrderStatusCommand(id, request.Status)))).WithName("UpdateOrderStatus").WithSummary("Cambiar estado de una orden").WithDescription("Permite Pending a Confirmed o Cancelled; otras transiciones devuelven 409.").Produces<Order>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(500); }
