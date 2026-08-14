using BuildingBlocks.Behaviors;
using FluentValidation;
using MongoDB.Driver;
using Ordering.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarter();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// CORS para permitir el frontend Vue.
// Cuando tengamos la URL definitiva del frontend,
// podemos restringirlo únicamente a esa URL.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var mongoConnection =
    builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDb:ConnectionString es obligatorio.");

builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(mongoConnection));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>()
        .GetDatabase(
            builder.Configuration["MongoDb:DatabaseName"]
            ?? "OrdersDb"));

builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

builder.Services.AddHttpClient<IBasketService, BasketService>(client =>
{
    var url =
        builder.Configuration["Services:BasketApi"]
        ?? throw new InvalidOperationException(
            "Services:BasketApi es obligatorio.");

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

var orders = app.Services
    .GetRequiredService<IMongoDatabase>()
    .GetCollection<Order>(
        builder.Configuration["MongoDb:OrdersCollection"]
        ?? "orders");

try
{
    await orders.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(x => x.IdempotencyKey),
            new CreateIndexOptions
            {
                Unique = true,
                Name = "ux_orders_idempotencyKey"
            }),

        new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(x => x.CustomerId),
            new CreateIndexOptions
            {
                Name = "ix_orders_customerId"
            }),

        new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Descending(x => x.CreatedAt),
            new CreateIndexOptions
            {
                Name = "ix_orders_createdAt"
            })
    ]);
}
catch (Exception exception)
{
    app.Logger.LogError(
        exception,
        "No fue posible verificar los índices de Orders al iniciar; las solicitudes usarán el manejador global seguro.");
}

app.UseCors("Frontend");

app.UseExceptionHandler();

// Disponible también en Render/Production.
app.MapOpenApi();

// Health check simple para Render.
app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "ok",
        service = "Ordering.API"
    }))
    .ExcludeFromDescription();

app.MapCarter();

app.Run();

public partial class Program;