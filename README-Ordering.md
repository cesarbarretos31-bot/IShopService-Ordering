# Ordering.API

Microservicio .NET 9 con Minimal APIs/Carter, CQRS/MediatR, FluentValidation y MongoDB Atlas. Catalog.API y Basket.API conservan PostgreSQL; Basket conserva además su configuración Redis. Ordering consulta Basket exclusivamente por HTTP.

## Arquitectura y datos

`POST /api/orders` exige `CustomerId == BasketId`, consulta `GET /basket/{basketId}` y comprueba también `Cart.UserName`. Los items se guardan como snapshot (`ProductName`, `UnitPrice`, `LineTotal`); no se recalculan desde Catalog. La tasa centralizada está en `Ordering:TaxRate` (0.16 por defecto). El basket no se elimina después de comprar.

MongoDB usa la base `OrdersDb`, colección `orders`, e índices estables: `ux_orders_idempotencyKey` único, `ix_orders_customerId` e `ix_orders_createdAt`.

Variables:

- `MongoDb__ConnectionString` (obligatoria y secreta)
- `MongoDb__DatabaseName` (por defecto `OrdersDb`)
- `MongoDb__OrdersCollection` (por defecto `orders`)
- `Services__BasketApi` (URL base pública o Docker de Basket.API)
- `Ordering__TaxRate` (por defecto `0.16`)

## Contratos

- `POST /api/orders`: body `{"customerId":"cesar","basketId":"cesar"}` y header `Idempotency-Key`. Devuelve 201 al crear y 200 al repetir la clave.
- `GET /api/orders/{id}`: 200/404.
- `GET /api/orders/customer/{customerId}`: 200, lista aislada por coincidencia exacta.
- `PATCH /api/orders/{id}/status`: body `{"status":"Confirmed"}`. Solo `Pending -> Confirmed` o `Pending -> Cancelled`; transición inválida 409.

Validación y basket vacío devuelven 400; orden inexistente 404; conflictos 409; fallos inesperados/Mongo 500 con detalle genérico. Si Atlas no está disponible durante el arranque se registra el fallo de índices, pero el proceso continúa para que las solicitudes demuestren el 500 controlado. El error real queda en logs del servidor. Swagger OpenAPI se publica en desarrollo mediante `/openapi/v1.json`.

## Ejecución

PowerShell local:

```powershell
$env:MongoDb__ConnectionString='mongodb+srv://...'
$env:Services__BasketApi='http://localhost:5073'
dotnet run --project src/Ordering/Ordering.API
```

Docker: copie `.env.example` a `.env`, coloque la URI real solo localmente y ejecute `docker compose up --build`. Ordering queda en `http://localhost:8082` y Basket en `http://localhost:8081`.

## Pruebas del examen

Use `Ordering.API.http`. P1 crea una orden; P2 consulta el id; P3 use un usuario con basket vacío; P4 repita la misma clave; P5 cambie Pending a Confirmed; P6 cancele una orden Pending y luego intente Confirmed; P7 configure temporalmente una URI Mongo inaccesible y compruebe un 500 sin detalles internos.

Para P-USER-1 guarde baskets `cesar` y `juan` usando el contrato existente y consulte `/basket/cesar` y `/basket/juan`; Marten usa `UserName` como identidad del documento. Para P-USER-2 cree órdenes con claves distintas y consulte cada ruta customer. P-USER-3 está incluido y debe devolver 400 antes de consultar Basket.

En Atlas cree un usuario con privilegios mínimos sobre `OrdersDb`, autorice solo las redes del hosting y configure la URI como secreto. En Render configure las cinco variables anteriores y la URL pública existente de Basket.API. La integración de compra/confirmación en Vue queda para la siguiente etapa.
