using System.Collections.Concurrent;
using FluentValidation;
using MongoDB.Driver;
using Ordering.API.Services;

namespace Ordering.API.Orders.CreateOrder;

public record CreateOrderCommand(
    string CustomerId,
    string BasketId,
    string IdempotencyKey)
    : ICommand<CreateOrderResult>;

public record CreateOrderResult(
    Order Order,
    bool Created);

public class CreateOrderValidator
    : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.BasketId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x)
            .Must(x =>
                string.Equals(
                    x.CustomerId,
                    x.BasketId,
                    StringComparison.Ordinal))
            .WithMessage(
                "CustomerId debe coincidir con BasketId.");
    }
}

public class CreateOrderCommandHandler(
    IOrderRepository repository,
    IBasketService baskets,
    IConfiguration configuration)
    : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    /*
     * Evita que dos solicitudes para el mismo Basket
     * ejecuten el checkout simultáneamente dentro
     * de esta instancia de Ordering.API.
     */
    private static readonly
        ConcurrentDictionary<string, SemaphoreSlim>
        CheckoutLocks = new(StringComparer.Ordinal);

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken ct)
    {
        var checkoutLock =
            CheckoutLocks.GetOrAdd(
                command.BasketId,
                _ => new SemaphoreSlim(1, 1));

        await checkoutLock.WaitAsync(ct);

        try
        {
            /*
             * PROTECCIÓN 1: IDEMPOTENCIA
             *
             * Si esta Idempotency-Key ya fue procesada,
             * devolvemos exactamente la orden existente
             * y NO creamos una nueva.
             */
            var existing =
                await repository.GetByIdempotencyKeyAsync(
                    command.IdempotencyKey,
                    ct);

            if (existing is not null)
            {
                ValidateExistingOrder(
                    existing,
                    command);

                /*
                 * Es posible que la orden haya sido creada
                 * correctamente en un intento anterior,
                 * pero haya fallado la eliminación del Basket.
                 *
                 * Intentamos completar esa parte.
                 *
                 * DeleteAsync debe considerar 404 como éxito.
                 */
                await baskets.DeleteAsync(
                    command.BasketId,
                    ct);

                return new CreateOrderResult(
                    existing,
                    false);
            }

            /*
             * Consultamos el carrito real mediante Basket.API.
             */
            var basket =
                await baskets.GetAsync(
                    command.BasketId,
                    ct);

            /*
             * El Basket debe pertenecer al mismo usuario.
             */
            if (!string.Equals(
                    basket.UserName,
                    command.BasketId,
                    StringComparison.Ordinal))
            {
                throw new BadRequestException(
                    "El basket no pertenece al cliente indicado.");
            }

            /*
             * No se permiten órdenes desde un Basket vacío.
             */
            if (basket.Items is null ||
                basket.Items.Count == 0)
            {
                throw new BadRequestException(
                    "No se puede crear una orden desde un basket vacío.");
            }

            /*
             * Validamos todos los productos antes de
             * construir la orden.
             */
            if (basket.Items.Any(x =>
                    x.ProductId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(
                        x.ProductName) ||
                    x.Quantity <= 0 ||
                    x.Price < 0))
            {
                throw new BadRequestException(
                    "El basket contiene productos inválidos.");
            }

            /*
             * PRICE SNAPSHOT
             *
             * Copiamos los datos del Basket hacia la orden.
             * Si posteriormente cambia el precio del catálogo,
             * esta orden conserva el precio de compra.
             */
            var items =
                basket.Items
                    .Select(x => new OrderItem
                    {
                        ProductId = x.ProductId,
                        ProductName = x.ProductName,
                        Quantity = x.Quantity,
                        UnitPrice = x.Price,
                        LineTotal =
                            x.Price * x.Quantity
                    })
                    .ToList();

            /*
             * Totales de la orden.
             */
            var subtotal =
                items.Sum(x => x.LineTotal);

            var rate =
                configuration.GetValue<decimal>(
                    "Ordering:TaxRate");

            var tax =
                decimal.Round(
                    subtotal * rate,
                    2,
                    MidpointRounding.AwayFromZero);

            /*
             * Construimos la nueva orden.
             */
            var order = new Order
            {
                CustomerId =
                    command.CustomerId,

                BasketId =
                    command.BasketId,

                CreatedAt =
                    DateTime.UtcNow,

                Status =
                    OrderStatus.Pending,

                Items =
                    items,

                Subtotal =
                    subtotal,

                Tax =
                    tax,

                Total =
                    subtotal + tax,

                IdempotencyKey =
                    command.IdempotencyKey
            };

            try
            {
                /*
                 * PASO 1:
                 * Persistimos la orden en MongoDB.
                 */
                var created =
                    await repository.CreateAsync(
                        order,
                        ct);

                /*
                 * PASO 2:
                 * Consumimos el carrito.
                 *
                 * Una vez que la compra quedó registrada,
                 * ese mismo Basket no debe permanecer
                 * disponible para otra compra.
                 */
                await baskets.DeleteAsync(
                    command.BasketId,
                    ct);

                return new CreateOrderResult(
                    created,
                    true);
            }
            catch (MongoWriteException ex)
                when (
                    ex.WriteError?.Category ==
                    ServerErrorCategory.DuplicateKey)
            {
                /*
                 * PROTECCIÓN 2:
                 *
                 * MongoDB tiene un índice UNIQUE sobre
                 * IdempotencyKey.
                 *
                 * Si dos solicitudes con la misma clave
                 * llegan casi al mismo tiempo, únicamente
                 * una consigue crear la orden.
                 */
                existing =
                    await repository
                        .GetByIdempotencyKeyAsync(
                            command.IdempotencyKey,
                            ct);

                if (existing is not null)
                {
                    ValidateExistingOrder(
                        existing,
                        command);

                    /*
                     * También terminamos de consumir
                     * el Basket en esta ruta.
                     */
                    await baskets.DeleteAsync(
                        command.BasketId,
                        ct);

                    return new CreateOrderResult(
                        existing,
                        false);
                }

                /*
                 * Si MongoDB reportó DuplicateKey,
                 * pero por alguna razón no pudimos
                 * recuperar la orden, dejamos que
                 * el manejador global procese el error.
                 */
                throw;
            }
        }
        finally
        {
            checkoutLock.Release();
        }
    }

    /*
     * Evita que alguien reutilice una Idempotency-Key
     * que pertenece a otra operación.
     */
    private static void ValidateExistingOrder(
        Order existing,
        CreateOrderCommand command)
    {
        var sameCustomer =
            string.Equals(
                existing.CustomerId,
                command.CustomerId,
                StringComparison.Ordinal);

        var sameBasket =
            string.Equals(
                existing.BasketId,
                command.BasketId,
                StringComparison.Ordinal);

        if (!sameCustomer ||
            !sameBasket)
        {
            throw new BadRequestException(
                "La Idempotency-Key ya fue utilizada para otra compra.");
        }
    }
}

public record CreateOrderRequest(
    string CustomerId,
    string BasketId);

public class CreateOrderEndpoint
    : ICarterModule
{
    public void AddRoutes(
        IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/orders",
                async (
                    CreateOrderRequest request,
                    HttpRequest http,
                    ISender sender) =>
                {
                    /*
                     * La clave es obligatoria.
                     * FluentValidation rechazará una cadena vacía.
                     */
                    var key =
                        http.Headers[
                                "Idempotency-Key"]
                            .FirstOrDefault()
                        ?? string.Empty;

                    var result =
                        await sender.Send(
                            new CreateOrderCommand(
                                request.CustomerId,
                                request.BasketId,
                                key));

                    /*
                     * 201 = realmente se creó.
                     * 200 = era un reintento idempotente
                     *       y devolvemos la misma orden.
                     */
                    return result.Created
                        ? Results.Created(
                            $"/api/orders/{result.Order.Id}",
                            result.Order)
                        : Results.Ok(
                            result.Order);
                })
            .WithName("CreateOrder")
            .WithSummary("Crear una orden")
            .WithDescription(
                "Crea una orden Pending desde el basket del mismo usuario. Idempotency-Key es obligatorio.")
            .Produces<Order>(
                StatusCodes.Status201Created)
            .Produces<Order>(
                StatusCodes.Status200OK)
            .ProducesProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError);
    }
}