# MS_Advanced – Come costruiscono DAVVERO una piattaforma a microservizi i pro (livello enterprise, .NET)

> Basato su: il tuo `MS_Architercure.md` (stato attuale di eCommerceSolution), i tuoi appunti in `CLAUDE.md`, e il README di SubSnap (`app-fullstack-substracker.live`), dove hai già Clean Architecture, DDD, Vertical Slice, MediatR pipeline behaviors, Outbox, JWT+refresh, Sentry, correlation-id.
> Obiettivo: portarti dal livello "corso" al livello "team enterprise".

---

## 0. Dove sei oggi vs dove sono i pro (gap analysis)

| Area | eCommerceSolution oggi | Come lo fanno i pro |
|---|---|---|
| Struttura interna mss | Layered (API / BLL / DAL) | **Vertical Slice** (servizi semplici) o **Clean + DDD** (servizi con dominio complesso) – *scelta per servizio* |
| Messaging | `RabbitMQ.Client` a mano, `autoAck: true` | **MassTransit** (o Wolverine) con **Transactional Outbox + Inbox**, retry, DLQ, idempotenza |
| Gateway | Ocelot | **YARP** (Microsoft) + pattern **BFF** |
| Chiamate tra mss | Orders → **gateway** → Users/Products | Chiamate **dirette** sulla rete interna (HTTP con **Refit** o **gRPC**); il gateway solo per traffico esterno |
| Resilienza | Polly v7 policies a mano | `Microsoft.Extensions.Http.Resilience` (Polly v8, *standard resilience handler*) |
| Cache | `IDistributedCache` Redis | **HybridCache** / **FusionCache** (L1 memoria + L2 Redis + backplane) |
| Osservabilità | log console | **OpenTelemetry** (traces + metrics + logs) → Grafana/Jaeger/Seq/Sentry, trace propagate anche dentro RabbitMQ |
| Health | nessuno | `/health/live` + `/health/ready` + `healthcheck` nel compose + `depends_on: condition: service_healthy` |
| Docker | 4 reti, porte DB pubblicate, niente volumi DB | reti a **tier** con `internal: true`, nessuna porta esposta tranne edge, **named volumes**, secrets, limiti risorse, profili |
| Auth | (non presente nei mss) | **Keycloak / Entra ID / Duende** OIDC, JWT validato in **ogni** servizio, non solo nel gateway |
| Config/segreti | env in chiaro nel compose | `.env` non committato, Docker secrets, Key Vault / Vault in prod |
| Deploy | docker-compose | compose solo per dev; prod su **Kubernetes** (Helm + ArgoCD/GitOps) o PaaS (Azure Container Apps, Railway, Fly) |
| Dev locale | compose | compose **oppure .NET Aspire** (orchestrazione locale + dashboard OTel gratis) |

---

## 1. Architettura di riferimento enterprise

```
                         Internet
                            │
                ┌───────────▼────────────┐
                │  CDN / WAF (Cloudflare,│   TLS, DDoS, cache statici
                │  Azure Front Door)     │
                └───────────┬────────────┘
                            │
                ┌───────────▼────────────┐       ┌────────────────────┐
                │  API Gateway / BFF     │──────►│ Identity Provider  │
                │  (YARP)                │ OIDC  │ Keycloak / Entra   │
                │  auth, rate limit,     │       └────────────────────┘
                │  routing, CORS         │
                └──┬────────┬────────┬───┘
     HTTP/gRPC     │        │        │          (rete "backend")
         ┌─────────▼─┐ ┌────▼─────┐ ┌▼──────────┐ ┌──────────────┐
         │ Catalog   │ │ Basket   │ │ Ordering  │ │ Payment ...  │   ← STATELESS
         │ (VSA)     │ │ (VSA)    │ │ (Clean+DDD│ │              │     N repliche
         └──┬────┬───┘ └──┬────┬──┘ └─┬────┬────┘ └──┬───────────┘
            │    │        │    │      │    │         │
            │    └────────┼────┴──────┼────┴─────────┘
            │             │           │       ▲ publish/consume (Outbox/Inbox)
            │             │           │  ┌────┴──────────────────┐
            │             │           │  │ Message Broker        │   ← STATEFUL
            │             │           │  │ RabbitMQ / Kafka /    │
            │             │           │  │ Azure Service Bus     │
            │             │           │  └───────────────────────┘
       ┌────▼───┐   ┌─────▼──┐   ┌────▼─────┐
       │Postgres│   │ Redis  │   │SQL Server│   ← STATEFUL, 1 DB per servizio
       │(Marten)│   │        │   │ (EF Core)│     reti private
       └────────┘   └────────┘   └──────────┘

   Trasversale:  OpenTelemetry Collector → Prometheus/Grafana, Tempo/Jaeger, Loki/Seq
                 Secret store (Vault / Key Vault) · Container registry · CI/CD · GitOps
```

**Regole d'oro che i senior applicano sempre:**
1. **Un servizio = un bounded context = un DB** (nessuno legge le tabelle di un altro, mai).
2. **Il gateway è stupido**: routing, auth, rate-limit. Niente logica di business.
3. **Sync solo per le query necessarie subito, async (eventi) per tutto il resto.**
4. **Nessuna transazione distribuita (2PC)**: si usano Outbox + Saga + consistenza eventuale.
5. **Servizi stateless, stato solo in componenti stateful dedicati** (DB, broker, cache, object storage).
6. **Osservabilità dal giorno 1** (non la aggiungi dopo: senza trace distribuite non debugghi un sistema distribuito).
7. **Contratti versionati** (API e messaggi) e backward-compatible.

---

## 2. Struttura della repository / soluzione

### Monorepo (scelta più comune in team piccoli-medi, e di dotnet/eShop)

```
eCommerce/
├─ Directory.Build.props          # TargetFramework, Nullable, TreatWarningsAsErrors, analyzers comuni
├─ Directory.Packages.props       # 🔥 Central Package Management: versioni NuGet in UN solo posto
├─ .editorconfig
├─ global.json                    # pinna la versione SDK
├─ docker-compose.yml             # infrastruttura + servizi (dev)
├─ docker-compose.override.yml    # porte host, volumi di sviluppo
├─ docker-compose.observability.yml
├─ deploy/
│  ├─ helm/ (o k8s/ con kustomize base + overlays dev/staging/prod)
├─ src/
│  ├─ BuildingBlocks/             # librerie condivise "tecniche" (NON di dominio)
│  │  ├─ BuildingBlocks/          #   CQRS abstractions, behaviors, exception handler, pagination
│  │  ├─ BuildingBlocks.Messaging/#   config MassTransit + IntegrationEvent base
│  │  └─ ServiceDefaults/         #   OpenTelemetry, health checks, resilience (stile Aspire)
│  ├─ Services/
│  │  ├─ Catalog/Catalog.API/                     # Vertical Slice + Carter + Marten
│  │  ├─ Basket/Basket.API/                       # Vertical Slice + Redis + Marten + gRPC client
│  │  ├─ Discount/Discount.Grpc/                  # gRPC + SQLite/EF Core
│  │  └─ Ordering/                                # Clean Architecture + DDD
│  │     ├─ Ordering.Domain/
│  │     ├─ Ordering.Application/
│  │     ├─ Ordering.Infrastructure/
│  │     └─ Ordering.API/
│  ├─ ApiGateways/YarpApiGateway/
│  └─ WebApps/Shopping.Web/       # consuma le API con Refit
└─ tests/
   ├─ Catalog.UnitTests/  Ordering.Domain.Tests/
   ├─ Ordering.IntegrationTests/   (Testcontainers)
   ├─ ArchitectureTests/           (NetArchTest)
   └─ ContractTests/               (Pact)
```

> 🔥 **Regola sulle librerie condivise**: si condivide solo codice **tecnico** (logging, behaviors, config bus). I **contratti dei messaggi** si condividono come pacchetto piccolo e versionato (`eCommerce.Contracts`) oppure si duplicano le classi in ogni servizio (molti team preferiscono duplicare per evitare accoppiamento). **Mai** condividere entità di dominio o DbContext.

### Polyrepo
Una repo per servizio + repo `platform`/`infra`. Si usa quando ci sono molti team indipendenti. Costo: versionamento dei pacchetti condivisi, coordinamento. Per imparare e per team < 30 persone: **monorepo**.

---

## 3. Architettura INTERNA di ogni servizio: si sceglie in base alla complessità

Non tutti i servizi meritano Clean Architecture + DDD. I pro scelgono **per servizio**:

| Servizio | Complessità | Architettura | Persistenza |
|---|---|---|---|
| Catalog | CRUD, letture tante | **Vertical Slice** + Carter + MediatR | **Marten** (Postgres come document DB) |
| Basket | stato temporaneo per utente | Vertical Slice + **decorator di cache** (Scrutor) | Redis (cache) + Marten |
| Discount | piccolo, chiamato sync ad alta frequenza | **gRPC** service | SQLite / EF Core |
| Ordering | regole di business, invarianti, stati | **Clean Architecture + DDD** + CQRS | SQL Server/Postgres + EF Core |

### 3.1 Vertical Slice (quello che hai già in SubSnap, applicato al mss)

```
Catalog.API/
├─ Products/
│  ├─ CreateProduct/
│  │  ├─ CreateProductEndpoint.cs   # Carter module
│  │  └─ CreateProductHandler.cs    # Command + Result + Validator + Handler nello stesso file
│  ├─ GetProducts/
│  ├─ GetProductById/
│  ├─ UpdateProduct/
│  └─ DeleteProduct/
├─ Models/Product.cs
├─ Data/CatalogInitialData.cs
└─ Program.cs
```

```csharp
// CreateProductHandler.cs
public record CreateProductCommand(string Name, List<string> Category, decimal Price)
    : ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id);

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

internal class CreateProductHandler(IDocumentSession session)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var product = cmd.Adapt<Product>();          // Mapster
        session.Store(product);                       // Marten
        await session.SaveChangesAsync(ct);
        return new CreateProductResult(product.Id);
    }
}

// CreateProductEndpoint.cs
public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) =>
        app.MapPost("/products", async (CreateProductRequest req, ISender sender) =>
        {
            var result = await sender.Send(req.Adapt<CreateProductCommand>());
            return Results.Created($"/products/{result.Id}", result.Adapt<CreateProductResponse>());
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();
}
```

### 3.2 Clean Architecture + DDD (Ordering) – identico a SubSnap ma con eventi di integrazione

```
Ordering.Domain/         Aggregates (Order), Entities (OrderItem), Value Objects (OrderId, Address, Payment),
                         Domain Events (OrderCreatedEvent), Exceptions — zero dipendenze
Ordering.Application/    Commands/Queries + Handlers, Behaviors, interfacce (IApplicationDbContext),
                         Domain Event Handlers che trasformano Domain Event → Integration Event
Ordering.Infrastructure/ EF Core DbContext, Configurations, Interceptors
                         (AuditableEntityInterceptor, DispatchDomainEventsInterceptor), Migrations
Ordering.API/            Carter endpoints, DI, exception handler
```

🔥 **Domain Event vs Integration Event** (differenza che distingue i senior):
- **Domain Event**: dentro il servizio, in-process (MediatR `INotification`), stessa transazione. Es. `OrderCreatedEvent`.
- **Integration Event**: esce dal servizio verso il broker, contratto pubblico versionato, va **sempre** via Outbox. Es. `OrderCreatedIntegrationEvent`.
- Flusso: Aggregate solleva Domain Event → `DispatchDomainEventsInterceptor` (EF `SaveChangesInterceptor`) lo pubblica con MediatR → handler crea Integration Event → `IPublishEndpoint.Publish` (MassTransit) → finisce nella tabella Outbox **nella stessa transazione** → background delivery verso RabbitMQ.

### 3.3 MediatR pipeline behaviors (hai già Validation/Logging/Performance/Transaction/Exception in SubSnap)
Nei mss si mettono in `BuildingBlocks` e ogni servizio li registra:
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
services.AddExceptionHandler<CustomExceptionHandler>();   // IExceptionHandler → ProblemDetails (RFC 7807)
```

### 3.4 Proxy / Decorator pattern per la cache (Basket)
```csharp
services.AddScoped<IBasketRepository, BasketRepository>();               // Marten
services.Decorate<IBasketRepository, CachedBasketRepository>();          // Scrutor: Redis davanti
```
`CachedBasketRepository` legge da Redis, se miss delega al repository reale. È il **Proxy/Decorator** dei tuoi appunti: il resto del codice non sa che c'è una cache (Open/Closed principle).

---

## 4. Le librerie che usano davvero (stack .NET 2025-2026)

> ⚠️ **Licenze (importante, cambiate nel 2025)**: **MediatR** (dalla v13), **AutoMapper** (dalla v15) e **MassTransit** (dalla v9) sono passati a licenza **commerciale** (gratis per uso personale/aziende piccole, a pagamento per le grandi). Le versioni precedenti (MediatR 12.x, AutoMapper 14.x, MassTransit 8.x) restano open-source. Per questo molti team stanno passando a **Mapster** (mapping), **Mediator** di martinothamar (source generator, gratis e più veloce) o **Wolverine**. Verifica sempre la licenza prima di usarle in un progetto commerciale.

| Categoria | Libreria standard "pro" | Alternative | Note |
|---|---|---|---|
| Endpoint | **Minimal APIs + Carter** | FastEndpoints, Controllers | Carter = moduli `ICarterModule`, perfetto con VSA |
| CQRS / Mediator | **MediatR** | Mediator (source-gen), Wolverine | pipeline behaviors = cross-cutting concerns |
| Validazione | **FluentValidation** | DataAnnotations | sempre in un `ValidationBehavior` |
| Mapping | **Mapster** | AutoMapper, Mapperly (source-gen) | Mapster: `obj.Adapt<T>()`, nessun profilo obbligatorio |
| ORM relazionale | **EF Core** | Dapper (letture veloci) | EF per write model, Dapper per read model "pesanti" |
| Document DB / Event Store | **Marten** (su PostgreSQL) | MongoDB driver, Cosmos DB | Marten: documenti JSONB + transazioni ACID + Event Sourcing |
| Messaging | **MassTransit** | Wolverine, NServiceBus, Rebus, Brighter | astrae RabbitMQ / Azure Service Bus / Amazon SQS / Kafka (Rider) |
| Kafka diretto | Confluent.Kafka | MassTransit Rider | |
| HTTP client tipizzato | **Refit** | `HttpClient` tipizzato, NSwag/Kiota client generati | interfaccia → client generato |
| RPC sync performante | **Grpc.AspNetCore** + Grpc.Net.ClientFactory | | interno tra mss, HTTP/2, protobuf |
| Gateway | **YARP** | Ocelot (meno mantenuto), Kong, Envoy, Azure APIM | |
| Resilienza | **Microsoft.Extensions.Http.Resilience** (Polly v8) | Polly diretto | `AddStandardResilienceHandler()` |
| Cache | **HybridCache** (.NET 9+), **FusionCache** | StackExchange.Redis, `IDistributedCache` | L1+L2, anti-stampede |
| Decorator / scan DI | **Scrutor** | | `services.Decorate<>()` |
| Observability | **OpenTelemetry** .NET | | traces/metrics/logs, esportatori OTLP |
| Logging | **Serilog** (o `ILogger` + OTel) | NLog | sink verso Seq / Elastic / Loki |
| Error tracking | Sentry | Application Insights | lo usi già in SubSnap |
| Health checks | **AspNetCore.HealthChecks.*** (Xabaril) | | NpgSql, Redis, RabbitMQ, SqlServer, UI |
| Auth | **Microsoft.AspNetCore.Authentication.JwtBearer** + Keycloak / Entra ID / Duende IdentityServer / OpenIddict | | |
| API versioning | **Asp.Versioning.Http** | | `/v1`, `/v2` |
| OpenAPI | Microsoft.AspNetCore.OpenApi + **Scalar** | Swashbuckle | dal .NET 9 Swashbuckle non è più nel template |
| Background jobs | Hangfire, **Quartz.NET**, `BackgroundService` | | con lock distribuito se più repliche |
| Feature flags | Microsoft.FeatureManagement | Unleash, LaunchDarkly | rilasci progressivi |
| Test | **xUnit**, **Testcontainers**, WireMock.Net, **NetArchTest**, Bogus, Respawn, FluentAssertions/Shouldly, **Pact** (contract), **k6**/NBomber (carico) | | |
| Dev locale | **.NET Aspire** | docker-compose, Tye (morto) | dashboard OTel, service discovery automatico |

---

## 5. Comunicazione: sync, async, Outbox, Saga

### 5.1 Quando usare cosa

| Caso | Scelta |
|---|---|
| Frontend → backend | HTTP/REST via Gateway (o GraphQL federation, raro) |
| Mss → mss, serve risposta subito, alta frequenza (es. Basket chiede sconto a Discount) | **gRPC** |
| Mss → mss, serve risposta subito, bassa frequenza | HTTP con **Refit** + resilience handler |
| "È successo X" (OrderPlaced, ProductPriceChanged) | **Evento** pubblicato su broker (pub/sub, fanout/topic) |
| "Fai X" verso UN servizio (ProcessPayment) | **Command** su coda (send, point-to-point) |
| Stream di eventi da rileggere / analytics / milioni di msg/s | **Kafka** |
| Workflow lungo multi-servizio | **Saga** (orchestration con MassTransit State Machine) |

### 5.2 gRPC tra mss (Basket → Discount)
```csharp
// Basket.API Program.cs
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(o =>
        o.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!))
    .AddStandardResilienceHandler();
```

### 5.3 Refit (Shopping.Web → Gateway, o Orders → Users)
```csharp
public interface IUsersApi
{
    [Get("/api/users/{id}")]
    Task<UserDto> GetUser(Guid id, CancellationToken ct = default);
}

builder.Services.AddRefitClient<IUsersApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://users-api:8080"))  // DIRETTO, non via gateway
    .AddStandardResilienceHandler();   // retry + circuit breaker + timeout + rate limiter già configurati
```

### 5.4 MassTransit + RabbitMQ con Outbox (sostituisce il tuo RabbitMQPublisher/Consumer a mano)
```csharp
// BuildingBlocks.Messaging/Extensions.cs
public static IServiceCollection AddMessageBroker<TDbContext>(this IServiceCollection services,
    IConfiguration config, Assembly? consumersAssembly = null) where TDbContext : DbContext
{
    services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        if (consumersAssembly is not null) x.AddConsumers(consumersAssembly);

        // 🔥 Transactional Outbox + Inbox (idempotenza) su EF Core
        x.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.UsePostgres();            // o UseSqlServer()
            o.UseBusOutbox();           // Publish() scrive in tabella, non sul broker
            o.QueryDelay = TimeSpan.FromSeconds(1);
        });

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(new Uri(config["MessageBroker:Host"]!), h =>
            {
                h.Username(config["MessageBroker:UserName"]!);
                h.Password(config["MessageBroker:Password"]!);
            });
            cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1),
                                                   TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
            cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
            cfg.ConfigureEndpoints(ctx);  // crea exchange/queue/binding in automatico
        });
    });
    return services;
}
```
Cosa ti dà MassTransit "gratis" rispetto al codice a mano:
- exchange **fanout per tipo di messaggio** + una coda per servizio consumer (*competing consumers*: 3 repliche di Orders si dividono i messaggi);
- **ack manuale** dopo che il consumer ha finito (niente più `autoAck: true`);
- retry immediati + **redelivery ritardata**, poi coda `*_error` (= **DLQ**) e `*_skipped`;
- **Outbox**: niente più "salvo su DB ma il publish fallisce" (dual-write problem);
- **Inbox / idempotenza**: se lo stesso messaggio arriva due volte non lo processi due volte;
- propagazione automatica di **trace OpenTelemetry** e `CorrelationId`;
- test harness in memoria (`AddMassTransitTestHarness`).

Consumer:
```csharp
public class ProductPriceChangedConsumer(HybridCache cache, ILogger<ProductPriceChangedConsumer> log)
    : IConsumer<ProductPriceChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProductPriceChangedIntegrationEvent> ctx)
    {
        await cache.RemoveAsync($"product:{ctx.Message.ProductId}", ctx.CancellationToken);
        log.LogInformation("Cache invalidated for {ProductId}", ctx.Message.ProductId);
    }
}
```

Contratto evento (versionato, immutabile, solo dati primitivi):
```csharp
public record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().AssemblyQualifiedName!;
}
public record ProductPriceChangedIntegrationEvent(Guid ProductId, decimal OldPrice, decimal NewPrice)
    : IntegrationEvent;
```
🔥 **Versionamento eventi**: aggiungi solo campi opzionali; se devi rompere il contratto pubblichi `...V2` in parallelo finché tutti i consumer hanno migrato.

### 5.5 Saga (orchestration) con MassTransit State Machine
```csharp
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State Submitted { get; private set; } = null!;
    public State StockReserved { get; private set; } = null!;
    public State Paid { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<StockReserved> StockReservedEvt { get; private set; } = null!;
    public Event<StockReservationFailed> StockFailed { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);
        Event(() => OrderSubmitted, e => e.CorrelateById(m => m.Message.OrderId));
        // ... correlate anche gli altri

        Initially(When(OrderSubmitted)
            .Then(c => c.Saga.Total = c.Message.Total)
            .Send(new Uri("queue:inventory-reserve-stock"), c => new ReserveStock(c.Saga.CorrelationId))
            .TransitionTo(Submitted));

        During(Submitted,
            When(StockReservedEvt).Send(c => new ProcessPayment(c.Saga.CorrelationId, c.Saga.Total)).TransitionTo(StockReserved),
            When(StockFailed).Publish(c => new OrderCancelled(c.Saga.CorrelationId)).TransitionTo(Failed));

        During(StockReserved,
            When(PaymentCompleted).Publish(c => new OrderConfirmed(c.Saga.CorrelationId)).TransitionTo(Paid).Finalize(),
            When(PaymentFailed)                                     // 🔥 COMPENSAZIONE
                .Send(c => new ReleaseStock(c.Saga.CorrelationId))
                .Publish(c => new OrderCancelled(c.Saga.CorrelationId))
                .TransitionTo(Failed));
    }
}
```
Lo stato della saga (`OrderState`) è persistito in DB (EF Core/Marten/Redis) → il servizio orchestratore resta **stateless**: qualunque replica può riprendere la saga.

### 5.6 Kafka: quando i pro lo aggiungono
- Event streaming / analytics / CDC (Debezium legge il WAL di Postgres e pubblica su Kafka – alternativa all'Outbox polling).
- Spesso convivono: **RabbitMQ/Service Bus per comandi e workflow**, **Kafka per lo stream di eventi di business** consumato da analytics/data platform.
- Concetti da padroneggiare: topic, **partizioni** (ordine garantito solo per partizione → usa `OrderId` come key), consumer group, offset commit, retention, compaction, schema registry (Avro/Protobuf).

### 5.7 Idempotenza ovunque
- Consumer: Inbox di MassTransit o tabella `processed_messages(message_id PK)`.
- API: header `Idempotency-Key` sui POST critici (pagamenti, creazione ordine) salvato in Redis/DB.
- "At-least-once delivery" è la realtà: **progetta ogni handler perché sia eseguibile 2 volte senza danni**.

---

## 6. API Gateway con YARP + pattern BFF

```csharp
// YarpApiGateway/Program.cs
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(o =>
    o.AddFixedWindowLimiter("fixed", opt => { opt.Window = TimeSpan.FromSeconds(10); opt.PermitLimit = 5; }));

builder.Services.AddAuthentication().AddJwtBearer(o => { o.Authority = "http://keycloak:8080/realms/ecommerce"; o.Audience = "gateway"; });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
```
```json
"ReverseProxy": {
  "Routes": {
    "catalog-route": {
      "ClusterId": "catalog-cluster",
      "Match": { "Path": "/catalog-service/{**catch-all}" },
      "Transforms": [ { "PathPattern": "{**catch-all}" } ]
    },
    "ordering-route": {
      "ClusterId": "ordering-cluster",
      "RateLimiterPolicy": "fixed",
      "AuthorizationPolicy": "default",
      "Match": { "Path": "/ordering-service/{**catch-all}" },
      "Transforms": [ { "PathPattern": "{**catch-all}" } ]
    }
  },
  "Clusters": {
    "catalog-cluster":  { "Destinations": { "d1": { "Address": "http://catalog-api:8080" } } },
    "ordering-cluster": { "Destinations": { "d1": { "Address": "http://ordering-api:8080" } },
                          "HealthCheck": { "Active": { "Enabled": true, "Path": "/health/ready" } } }
  }
}
```
- **Perché YARP > Ocelot**: mantenuto da Microsoft, performance altissime, pipeline ASP.NET Core standard (puoi usare qualunque middleware), config hot-reload, load-balancing e health check attivi.
- **BFF (Backend For Frontend)**: un gateway **per tipo di client** (web, mobile) che fa anche aggregazione delle chiamate e tiene i token lato server (cookie httpOnly verso il browser, come fai già in SubSnap con Next.js Server Actions!). Next.js server-side di SubSnap È già un BFF.
- Il gateway **non** sostituisce l'autorizzazione nei servizi: ogni mss valida comunque il JWT (*zero trust*).

---

## 7. Docker da professionisti

### 7.1 Dockerfile multi-stage (sicuro e piccolo)
```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# 🔥 copia prima solo i csproj → layer di restore in cache finché non cambi i pacchetti
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Services/Ordering/Ordering.API/Ordering.API.csproj src/Services/Ordering/Ordering.API/
COPY src/Services/Ordering/Ordering.Application/Ordering.Application.csproj src/Services/Ordering/Ordering.Application/
COPY src/Services/Ordering/Ordering.Domain/Ordering.Domain.csproj src/Services/Ordering/Ordering.Domain/
COPY src/Services/Ordering/Ordering.Infrastructure/Ordering.Infrastructure.csproj src/Services/Ordering/Ordering.Infrastructure/
COPY src/BuildingBlocks/ src/BuildingBlocks/
RUN dotnet restore src/Services/Ordering/Ordering.API/Ordering.API.csproj
COPY . .
RUN dotnet publish src/Services/Ordering/Ordering.API/Ordering.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# 🔥 immagine runtime "chiseled": niente shell, niente package manager, utente non-root di default
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Ordering.API.dll"]
```
Alternativa senza Dockerfile: `dotnet publish /t:PublishContainer` (container nativi dell'SDK .NET).
Extra pro: scansione vulnerabilità (Trivy / Docker Scout) in CI, tag immutabili (`:1.4.2` o `:sha-abc123`, mai solo `:latest` in prod), `.dockerignore` curato.

### 7.2 docker-compose.yml "enterprise" – reti a livelli

🔥 Sì, i pro usano **più reti**, e lo fanno per **tier/sicurezza**, esattamente come hai iniziato a fare tu:

| Rete | Chi c'è | Perché |
|---|---|---|
| `edge` | gateway (+ identity) | l'unica cosa raggiungibile dall'esterno |
| `backend` (`internal: true`) | gateway, tutti i mss, identity | traffico service-to-service, **nessun accesso a Internet** |
| `messaging` (`internal: true`) | mss che usano il bus, broker | solo chi deve pubblicare/consumare vede RabbitMQ |
| `<servizio>-data` (`internal: true`) | 1 mss + il SUO db | database-per-service a livello di rete |
| `cache` (`internal: true`) | mss che usano Redis + Redis | |
| `observability` | tutti i mss + collector OTel + Grafana/Jaeger/Seq | telemetria separata dal traffico di business |

```yaml
# docker-compose.yml
name: ecommerce

x-dotnet-service: &dotnet-service           # 🔥 YAML anchor: config comune a tutti i mss
  restart: unless-stopped
  environment: &dotnet-env
    ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Development}
    ASPNETCORE_HTTP_PORTS: "8080"
    OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
  healthcheck:
    # immagine aspnet standard: installa curl nel Dockerfile. Con immagini chiseled (niente shell/curl)
    # si usa un piccolo binario di probe copiato nell'immagine, oppure ci si affida alle probe di K8s.
    test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
    interval: 10s
    timeout: 3s
    retries: 5
    start_period: 20s
  deploy:
    resources:
      limits: { cpus: "0.50", memory: 512M }
  logging:
    driver: json-file
    options: { max-size: "10m", max-file: "3" }

services:

  # ---------------- EDGE ----------------
  yarpapigateway:
    <<: *dotnet-service
    build: { context: ., dockerfile: src/ApiGateways/YarpApiGateway/Dockerfile }
    image: ${REGISTRY:-local}/yarp-gateway:${TAG:-dev}
    ports: ["6064:8080"]                   # 🔥 UNICA porta pubblicata in "prod-like"
    networks: [edge, backend, observability]
    depends_on:
      catalog-api:  { condition: service_healthy }
      ordering-api: { condition: service_healthy }

  # ---------------- SERVIZI (stateless) ----------------
  catalog-api:
    <<: *dotnet-service
    build: { context: ., dockerfile: src/Services/Catalog/Catalog.API/Dockerfile }
    image: ${REGISTRY:-local}/catalog-api:${TAG:-dev}
    environment:
      <<: *dotnet-env
      ConnectionStrings__Database: Host=catalogdb;Port=5432;Database=CatalogDb;Username=postgres;Password_File=/run/secrets/catalogdb_password
    secrets: [catalogdb_password]
    networks: [backend, catalog-data, messaging, observability]
    depends_on:
      catalogdb:     { condition: service_healthy }
      messagebroker: { condition: service_healthy }

  basket-api:
    <<: *dotnet-service
    build: { context: ., dockerfile: src/Services/Basket/Basket.API/Dockerfile }
    image: ${REGISTRY:-local}/basket-api:${TAG:-dev}
    environment:
      <<: *dotnet-env
      ConnectionStrings__Redis: redis:6379
      GrpcSettings__DiscountUrl: http://discount-grpc:8080
    networks: [backend, basket-data, cache, messaging, observability]
    depends_on:
      basketdb: { condition: service_healthy }
      redis:    { condition: service_healthy }

  ordering-api:
    <<: *dotnet-service
    build: { context: ., dockerfile: src/Services/Ordering/Ordering.API/Dockerfile }
    image: ${REGISTRY:-local}/ordering-api:${TAG:-dev}
    networks: [backend, ordering-data, messaging, observability]
    depends_on:
      orderingdb:          { condition: service_healthy }
      messagebroker:       { condition: service_healthy }
      ordering-migrations: { condition: service_completed_successfully }   # 🔥 migrazioni come job separato

  ordering-migrations:                     # job one-shot: applica le migration EF e termina
    image: ${REGISTRY:-local}/ordering-migrations:${TAG:-dev}
    build: { context: ., dockerfile: src/Services/Ordering/Ordering.Migrations/Dockerfile }
    restart: "no"
    networks: [ordering-data]
    depends_on:
      orderingdb: { condition: service_healthy }

  # ---------------- STATEFUL ----------------
  catalogdb:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: CatalogDb
      POSTGRES_PASSWORD_FILE: /run/secrets/catalogdb_password
    secrets: [catalogdb_password]
    volumes: [catalogdb-data:/var/lib/postgresql/data]       # 🔥 named volume: i dati sopravvivono
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      retries: 10
    networks: [catalog-data]                                  # visibile SOLO a catalog-api

  basketdb:
    image: postgres:16-alpine
    environment: { POSTGRES_DB: BasketDb, POSTGRES_PASSWORD: ${BASKETDB_PASSWORD} }
    volumes: [basketdb-data:/var/lib/postgresql/data]
    healthcheck: { test: ["CMD-SHELL", "pg_isready -U postgres"], interval: 5s, retries: 10 }
    networks: [basket-data]

  orderingdb:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment: { ACCEPT_EULA: "Y", MSSQL_SA_PASSWORD: ${ORDERINGDB_PASSWORD} }
    volumes: [orderingdb-data:/var/opt/mssql]
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1'"]
      interval: 10s
      retries: 10
    networks: [ordering-data]

  redis:
    image: redis:7-alpine
    command: ["redis-server", "--appendonly", "yes", "--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru"]
    volumes: [redis-data:/data]
    healthcheck: { test: ["CMD", "redis-cli", "ping"], interval: 5s, retries: 10 }
    networks: [cache]

  messagebroker:
    image: rabbitmq:3.13-management-alpine
    hostname: messagebroker                  # 🔥 RabbitMQ salva i dati per hostname: deve essere stabile
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes: [rabbitmq-data:/var/lib/rabbitmq]
    healthcheck: { test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"], interval: 10s, retries: 10 }
    networks: [messaging]

  # ---------------- OBSERVABILITY (opzionale) ----------------
  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.110.0
    profiles: [observability]                 # 🔥 parte solo con --profile observability
    volumes: [./deploy/otel/collector.yaml:/etc/otelcol-contrib/config.yaml:ro]
    networks: [observability]

  grafana-lgtm:                              # Grafana + Loki + Tempo + Prometheus in 1 container (dev)
    image: grafana/otel-lgtm:0.8.1
    profiles: [observability]
    ports: ["3000:3000"]
    networks: [observability]

networks:
  edge:            {}
  backend:         { internal: true }
  messaging:       { internal: true }
  cache:           { internal: true }
  catalog-data:    { internal: true }
  basket-data:     { internal: true }
  ordering-data:   { internal: true }
  observability:   {}

volumes:
  catalogdb-data:
  basketdb-data:
  orderingdb-data:
  redis-data:
  rabbitmq-data:

secrets:
  catalogdb_password:
    file: ./secrets/catalogdb_password.txt     # in .gitignore
```

```yaml
# docker-compose.override.yml  (solo DEV: applicato automaticamente da `docker compose up`)
services:
  catalog-api:   { ports: ["6000:8080"] }      # Swagger dal PC
  ordering-api:  { ports: ["6003:8080"] }
  catalogdb:     { ports: ["5432:5432"] }      # DBeaver
  messagebroker: { ports: ["15672:15672"] }    # UI RabbitMQ
```
> ⚠️ Nota tecnica: una rete `internal: true` blocca anche il **port publishing** verso l'host per i container che stanno SOLO su quella rete. In dev, se vuoi raggiungere un DB dal PC, o lo metti anche su una rete non-internal, o togli `internal` nell'override. Per questo i pro tengono `internal: true` nella config "prod-like" e lo rilassano in dev.

Comandi che usano i pro:
```bash
docker compose up -d --build
docker compose --profile observability up -d
docker compose -f docker-compose.yml -f docker-compose.prod.yml config   # vedi la config finale mergiata
docker compose up -d --scale catalog-api=3                              # 3 repliche stateless (senza ports fissi!)
docker compose logs -f ordering-api
docker compose down          # mantiene i volumi
docker compose down -v       # ⚠️ cancella anche i dati
```

---

## 8. 🔥🔥 STATELESS vs STATEFUL – come li gestiscono davvero

### 8.1 Definizione operativa
- **Stateless**: puoi **uccidere** il container in qualsiasi momento, avviarne 10 copie, mandare la richiesta a una qualunque, e **nessun dato si perde** né cambia il risultato. Tutto lo stato vive *fuori* (DB, cache, broker, object storage).
- **Stateful**: il container possiede dati su disco o in memoria che **non** sono ricostruibili gratis (DB, broker, Redis con persistenza, Elasticsearch, Qdrant...). Ha bisogno di **identità stabile**, **storage persistente**, ordine di avvio, backup, replica.

### 8.2 Checklist per rendere un mss DAVVERO stateless (i pro la applicano a ogni servizio .NET)
| ❌ Stato nascosto che rompe lo scaling | ✅ Soluzione |
|---|---|
| Sessione in memoria (`AddSession` con memoria locale) | JWT stateless, o sessione in Redis |
| `IMemoryCache` usato come fonte di verità | `IMemoryCache` solo come L1 riscaldabile + Redis come L2 (**HybridCache/FusionCache** con backplane per invalidare tutte le repliche) |
| File caricati salvati su disco del container | **Object storage** (S3, Azure Blob, R2, Hetzner – come fai in SubSnap con presigned URL) |
| **Data Protection keys** in locale (cookie/antiforgery invalidi tra repliche!) | `AddDataProtection().PersistKeysToStackExchangeRedis(...)` o Azure Blob + Key Vault |
| SignalR con più repliche | **Redis backplane** o Azure SignalR Service |
| `BackgroundService` che fa job periodici (es. outbox processor, cron) | con N repliche gira N volte → **lock distribuito** (Postgres advisory lock, RedLock, `DistributedLock` lib), **leader election**, o Quartz in modalità cluster, o job separato |
| Rate limiting in memoria | rate limiting nel gateway o con store Redis |
| Migrazioni EF `Database.Migrate()` allo startup | con 3 repliche partono 3 migrazioni in parallelo → **job di migrazione separato** (EF migration bundle, init container, step CI/CD) |
| Config in file dentro l'immagine | **env vars** / config server / Key Vault (12-factor) |
| Contatori / stati in campi `static` | nel DB/Redis |
| Shutdown brutale | **graceful shutdown**: gestire `SIGTERM`, `HostOptions.ShutdownTimeout`, finire i messaggi in corso, readiness che va "unhealthy" prima di fermarsi |

Il "manifesto" dietro: **The Twelve-Factor App** (processi stateless, config in env, backing services come risorse collegate, disposability, dev/prod parity, log come stream su stdout).

### 8.3 Come SCALANO i stateless
- **Compose (dev)**: `--scale servizio=N` (il DNS Docker fa round-robin; non usare `ports` fissi sul servizio scalato).
- **Kubernetes**: `Deployment` + `ReplicaSet` + `Service` (ClusterIP, load balancing) + **HPA** (autoscaling su CPU/memoria) o **KEDA** (autoscaling su **lunghezza della coda RabbitMQ/Kafka** → i consumer scalano in base ai messaggi in attesa; anche a zero).
- **PaaS**: Azure Container Apps (KEDA integrato), Railway/Fly replicas.
- Rilasci: **rolling update** (zero downtime grazie a readiness probe), **blue/green**, **canary** (Argo Rollouts, Flagger, service mesh).

```yaml
# K8s: un mss stateless
apiVersion: apps/v1
kind: Deployment
metadata: { name: ordering-api }
spec:
  replicas: 3
  selector: { matchLabels: { app: ordering-api } }
  template:
    metadata: { labels: { app: ordering-api } }
    spec:
      containers:
        - name: ordering-api
          image: myregistry.azurecr.io/ordering-api:1.4.2
          ports: [{ containerPort: 8080 }]
          envFrom: [{ configMapRef: { name: ordering-config } }, { secretRef: { name: ordering-secrets } }]
          resources:
            requests: { cpu: 100m, memory: 256Mi }
            limits:   { memory: 512Mi }
          readinessProbe: { httpGet: { path: /health/ready, port: 8080 }, periodSeconds: 5 }
          livenessProbe:  { httpGet: { path: /health/live,  port: 8080 }, periodSeconds: 10 }
          startupProbe:   { httpGet: { path: /health/live,  port: 8080 }, failureThreshold: 30, periodSeconds: 2 }
          securityContext: { runAsNonRoot: true, readOnlyRootFilesystem: true, allowPrivilegeEscalation: false }
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata: { name: ordering-api }
spec:
  scaleTargetRef: { apiVersion: apps/v1, kind: Deployment, name: ordering-api }
  minReplicas: 2
  maxReplicas: 10
  metrics: [{ type: Resource, resource: { name: cpu, target: { type: Utilization, averageUtilization: 70 } } }]
```

### 8.4 Come GESTISCONO gli stateful (le 3 strade)

**Strada 1 – Managed service (🔥 la scelta n°1 dei pro in produzione)**
Il DB/broker NON gira nel tuo cluster. Lo compri come servizio: Azure Database for PostgreSQL / Azure SQL / Cosmos DB, Amazon RDS / Aurora, Azure Cache for Redis / Upstash / ElastiCache, Azure Service Bus / Amazon MQ / CloudAMQP / Confluent Cloud (Kafka), Qdrant Cloud / Pinecone.
Ti dà: backup automatici, point-in-time restore, replica, failover, patching, scaling. È esattamente quello che scrivevi nei tuoi appunti per Qdrant. Nel cluster restano **solo i servizi stateless**. Lo stato è "qualcun altro che lo gestisce".

**Strada 2 – Stateful su Kubernetes con Operator**
Se devi farli girare tu (on-prem, costi, compliance):
- `StatefulSet`: pod con **nome stabile** (`postgres-0`, `postgres-1`), avvio ordinato, ognuno col **suo** `PersistentVolumeClaim` (disco che sopravvive al pod), **headless Service** per indirizzare la singola replica.
- **Operator** = software che sa fare il DBA al posto tuo (failover, backup, upgrade): **CloudNativePG** (Postgres), **RabbitMQ Cluster Operator**, **Strimzi** (Kafka), Redis Operator / Redis Sentinel, MongoDB Community Operator.
- Mai un `Deployment` con 3 repliche di Postgres sullo stesso volume: corrompi i dati.

```yaml
# CloudNativePG: un cluster Postgres HA in 10 righe
apiVersion: postgresql.cnpg.io/v1
kind: Cluster
metadata: { name: ordering-db }
spec:
  instances: 3                          # 1 primary + 2 replica, failover automatico
  storage: { size: 20Gi }
  backup:
    barmanObjectStore: { destinationPath: s3://backups/ordering, s3Credentials: { ... } }
```

**Strada 3 – Docker Compose / VM singola (dev, staging, piccoli progetti)**
Named volumes + backup schedulati (`pg_dump` in un container cron → object storage) + `restart: unless-stopped`. Ok per Railway/Fly con volumi, non è HA.

### 8.5 Tabella riassuntiva

| | Stateless (mss .NET, gateway) | Stateful (DB, broker, cache persistente) |
|---|---|---|
| Identità | intercambiabili (cattle) | identità stabile (`db-0`) |
| Storage | nessuno / effimero, `readOnlyRootFilesystem` | volume persistente (PVC / named volume) |
| Scaling | orizzontale, HPA/KEDA, anche a 0 | verticale o replica con protocollo (primary/replica, quorum, partizioni) |
| Deploy | rolling / canary, frequente | raro, con piano, backup prima |
| K8s object | Deployment | StatefulSet + Operator, o **managed fuori dal cluster** |
| Failure | ricrea il pod, nessuna perdita | failover, restore da backup, RPO/RTO |
| Compose | `--scale`, niente ports fissi | `volumes:`, `healthcheck`, `hostname` stabile |

### 8.6 Stato "ibrido" dentro i mss: le tecniche avanzate
- **Saga state, outbox, inbox** → nel DB del servizio (il processo resta stateless).
- **Event Sourcing** (Marten): lo stato dell'aggregato è la somma degli eventi salvati; le *projections* creano read model. Il servizio resta stateless, lo store no.
- **Cache-aside + invalidazione via eventi** (quello che fai già con Redis + RabbitMQ in Orders!) → con FusionCache aggiungi il **backplane** per invalidare la L1 di tutte le repliche.
- **Read model locale** alimentato da eventi (Orders tiene una copia di `ProductName`, `UserEmail`) → zero chiamate sync, consistenza eventuale.

---

## 9. Gestione dei dati distribuiti

- **Database per service** + **polyglot persistence** (ognuno sceglie il DB giusto: Marten per documenti, SQL Server per transazioni, Redis per basket, Elasticsearch per ricerca).
- **CQRS**: write model normalizzato (EF Core) + read model denormalizzato (Dapper/viste/projection Marten/Elastic). Puoi anche avere DB separati per read e write.
- **Query cross-servizio**: *API Composition* (il BFF/gateway chiama più servizi e unisce) oppure *read model replicato* via eventi. Mai JOIN tra DB di servizi diversi.
- **Transazioni cross-servizio**: Saga (choreography per 2-3 passi, orchestration per flussi lunghi) + **compensazioni** (non esiste rollback, esiste "annulla con un'azione inversa").
- **Consistenza eventuale** esposta anche alla UI: stati `Pending`, `Confirmed`, `Cancelled` (come nei tuoi appunti).
- **ID generati dal client/servizio** (GUID v7 / ULID) invece di identity del DB → puoi pubblicare eventi prima del round-trip e sono ordinabili. `Guid.CreateVersion7()` dal .NET 9.

---

## 10. Osservabilità (non opzionale)

I 3 segnali: **traces** (il viaggio di una richiesta attraverso gateway → Orders → RabbitMQ → consumer), **metrics** (RPS, latenza p95/p99, errori, lunghezza code), **logs** strutturati con `TraceId`.

```csharp
// ServiceDefaults/Extensions.cs  (stile .NET Aspire, condiviso da tutti i mss)
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("MassTransit")              // 🔥 trace attraverso il broker
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("MassTransit")
            .AddOtlpExporter());
    builder.Logging.AddOpenTelemetry(l => { l.IncludeScopes = true; l.IncludeFormattedMessage = true; l.AddOtlpExporter(); });

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });
    return builder;
}

public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    app.MapHealthChecks("/health/live", new() { Predicate = r => r.Tags.Contains("live") });
    app.MapHealthChecks("/health/ready");          // include DB, Redis, RabbitMQ
    return app;
}
```
Ogni servizio poi aggiunge le sue dipendenze al readiness:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connString)
    .AddRedis(redisConn)
    .AddRabbitMQ();
```
- **liveness** = "il processo è vivo" (NON controllare il DB qui, altrimenti se il DB cade K8s ti riavvia tutti i pod inutilmente).
- **readiness** = "posso ricevere traffico" (qui sì DB, broker).
- Stack: OTel Collector → **Prometheus + Grafana** (metrics), **Tempo/Jaeger** (traces), **Loki/Seq/Elastic** (logs); oppure tutto in Azure Monitor/App Insights, Datadog, Grafana Cloud. Sentry per errori (come SubSnap).
- Poi: **SLO** (es. 99.9% delle richieste < 300 ms), alert su burn-rate, dashboard RED (Rate, Errors, Duration) per servizio.

---

## 11. Sicurezza

- **Identity Provider centralizzato** (Keycloak in container per dev, Entra ID/Auth0/Duende in prod): OAuth2 + OIDC. Gli utenti fanno login lì, non nel servizio Users. (Users diventa "profilo utente", non "autenticazione").
- **JWT validato in OGNI mss** (`AddJwtBearer` con `Authority`), policy per scope/ruoli; il gateway fa solo il primo controllo.
- **Service-to-service**: client credentials flow (token per servizio) o **mTLS** tramite service mesh (Linkerd, Istio) → *zero trust*.
- **Segreti**: mai nel compose committato; `.env` + Docker secrets in dev, **Azure Key Vault / HashiCorp Vault / K8s Secrets + External Secrets Operator** in prod.
- **Network policies** in K8s (equivalente delle tue reti compose): "solo ordering-api può parlare con ordering-db".
- Container non-root, immagini chiseled/distroless, scansioni CVE, SBOM, firma immagini (cosign).
- Rate limiting + WAF all'edge, CORS solo al gateway.

---

## 12. Resilienza (evoluzione dei tuoi appunti Polly)

```csharp
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(c => c.BaseAddress = new("http://catalog-api:8080"))
    .AddStandardResilienceHandler(o =>
    {
        o.Retry.MaxRetryAttempts = 3;                       // exponential backoff + jitter
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2); // timeout per tentativo
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });
```
Pipeline standard (in ordine): Rate limiter → Total timeout → Retry → Circuit breaker → Attempt timeout.
Altre tecniche: **hedging** (`AddStandardHedgingHandler`: manda una seconda richiesta parallela se la prima tarda), **fallback** con dati da cache, **bulkhead** (concurrency limiter), **load shedding** (rifiuta presto con 503 invece di morire), **timeouts ovunque** (anche DB e broker), **retry solo su operazioni idempotenti**.

---

## 13. Testing in un sistema a microservizi

| Livello | Cosa | Tool |
|---|---|---|
| Unit | Domain (aggregati, value objects), handler | xUnit, NSubstitute/Moq, Bogus |
| Architettura | "Domain non referenzia Infrastructure", "handler sono internal/sealed" | **NetArchTest** / ArchUnitNET |
| Integration | API + DB **reale** in container | `WebApplicationFactory` + **Testcontainers** (Postgres, Redis, RabbitMQ) + Respawn |
| Messaging | consumer/saga | `MassTransit.Testing` test harness |
| Contract | "il consumer si aspetta questo JSON, il provider lo rispetta" | **Pact** (evita di rompere altri team) |
| HTTP esterni | finto Users/Payment | WireMock.Net |
| E2E | pochi scenari critici | Playwright |
| Carico | p95 sotto carico, autoscaling | **k6**, NBomber |
| Chaos | uccidere pod/container, latenza iniettata | Chaos Mesh, Simmy (Polly) |

---

## 14. CI/CD e deploy

```
push ─► GitHub Actions (path filter: solo i servizi cambiati)
        ├─ dotnet build + test (unit, arch, integration con Testcontainers)
        ├─ docker build → scan Trivy → push su registry (GHCR / ACR) con tag = git sha
        └─ aggiorna il tag nel repo GitOps (Helm values / kustomize)
                                   │
                                   ▼
                     ArgoCD / Flux (GitOps) sincronizza il cluster
                     ├─ dev (auto)  ├─ staging (auto + test E2E)  └─ prod (approvazione + canary)
```
- **Ogni servizio ha la sua pipeline e la sua versione** (deploy indipendente = vero beneficio dei mss).
- **Infra as Code**: Terraform / Bicep / Pulumi per cluster, DB managed, broker, Key Vault.
- **Helm chart** comune per tutti i mss .NET (un template, values diversi).
- Alternative a K8s puro: **Azure Container Apps** (K8s + KEDA + Dapr gestiti, ottimo compromesso), Railway/Fly (come nei tuoi appunti) per partire.
- **.NET Aspire**: in dev sostituisce il compose (AppHost in C# che dichiara servizi, DB, Redis, RabbitMQ; dashboard OTel inclusa) e può generare manifest per il deploy (`azd`, Kubernetes via `aspirate`/publisher).

```csharp
// AppHost/Program.cs  (.NET Aspire)
var builder = DistributedApplication.CreateBuilder(args);
var postgres = builder.AddPostgres("pg").WithDataVolume();
var catalogDb = postgres.AddDatabase("catalogdb");
var redis = builder.AddRedis("redis");
var rabbit = builder.AddRabbitMQ("messaging").WithManagementPlugin();

var catalog = builder.AddProject<Projects.Catalog_API>("catalog-api").WithReference(catalogDb).WithReference(rabbit);
var basket  = builder.AddProject<Projects.Basket_API>("basket-api").WithReference(redis).WithReference(rabbit);
builder.AddProject<Projects.YarpApiGateway>("gateway").WithReference(catalog).WithReference(basket).WithExternalHttpEndpoints();
builder.Build().Run();
```

---

## 15. Pattern "pro" in un colpo d'occhio (glossario da sapere a memoria)

| Pattern | Problema che risolve |
|---|---|
| API Gateway / BFF | un ingresso per client, auth, routing, aggregazione |
| Database per Service | autonomia, deploy indipendente |
| CQRS | letture e scritture con modelli/scaling diversi |
| Event Sourcing | storico completo, audit, projections |
| Transactional Outbox | dual-write DB + broker |
| Inbox / Idempotent Consumer | messaggi duplicati (at-least-once) |
| Saga (choreography / orchestration) | transazioni distribuite + compensazioni |
| Competing Consumers | scalare i consumer di una coda |
| Dead Letter Queue | messaggi "avvelenati" |
| Circuit Breaker / Retry / Timeout / Bulkhead / Hedging | fault tolerance |
| Cache-aside + invalidation by events | performance con dati di altri servizi |
| Strangler Fig | migrare un monolite pezzo per pezzo |
| Anti-Corruption Layer | proteggere il tuo dominio dal modello di un altro servizio/legacy |
| Sidecar / Service Mesh / Dapr | mTLS, retry, discovery fuori dal codice |
| Health Check API (live/ready) | orchestratore sa quando riavviare/instradare |
| Externalized Configuration | stessa immagine in tutti gli ambienti |
| Distributed Tracing + Correlation ID | debug cross-servizio |
| Consumer-Driven Contracts | non rompere i consumer |
| Leader Election / Distributed Lock | job singleton con N repliche |

---

## 16. Roadmap concreta per il TUO eCommerceSolution

**Step 1 – Igiene (1 giorno)**
1. Correggi `ApiGatway/Dockerfile` → `ApiGateway/Dockerfile`, `ASPNETCORE_HTTP_ENVIRONMENT` → `ASPNETCORE_ENVIRONMENT`, nome exchange coerente.
2. Named volumes per Mongo/MySQL/Postgres/RabbitMQ; `.env` per le password; togli le porte DB nel compose base e mettile in `docker-compose.override.yml`.
3. `healthcheck` su tutti i container + `depends_on: condition: service_healthy` (così sparisce il loop "riprovo ogni 5 s").

**Step 2 – Robustezza messaging**
4. Sostituisci `RabbitMQ.Client` a mano con **MassTransit** (8.x se vuoi restare open-source): eventi `ProductUpdatedIntegrationEvent` / `ProductDeletedIntegrationEvent`, retry, DLQ, ack automatico corretto.
5. **Outbox** in Products (EF Core/MySQL) – stesso concetto che hai già in SubSnap, ma fatto da MassTransit.

**Step 3 – Comunicazione**
6. Orders chiama Users/Products **direttamente** (`users-microservice:9090`) con **Refit** + `AddStandardResilienceHandler()`; il gateway solo per il frontend.
7. Ocelot → **YARP** (con rate limiting ASP.NET Core).
8. Users pubblica `UserUpdatedIntegrationEvent` → Orders aggiorna la cache/read model come già fa con i prodotti.

**Step 4 – Osservabilità e sicurezza**
9. Progetto `ServiceDefaults` con OpenTelemetry + health checks; container `grafana/otel-lgtm` con profilo `observability`. Vedrai la trace Angular → gateway → Orders → Products → RabbitMQ → consumer in un'unica timeline.
10. Keycloak in compose + JWT su ogni servizio.

**Step 5 – Architettura interna**
11. Nuovo servizio **Basket** in Vertical Slice + Carter + Marten + Redis decorator (Scrutor) + gRPC verso un nuovo **Discount**.
12. Riscrivi **Orders** in Clean + DDD (Domain events → Integration events via Outbox) e aggiungi la **Saga** di checkout: Basket → Ordering → Inventory → Payment con compensazioni.

**Step 6 – Produzione**
13. Dockerfile chiseled non-root, GitHub Actions per servizio, registry GHCR.
14. Deploy: Railway/Azure Container Apps (stateless) + DB/Redis/broker **managed** (stateful). Poi, per imparare K8s: kind/k3d in locale → Helm chart → AKS con CloudNativePG o DB managed + KEDA sulle code.

---

## 17. Repo e risorse di riferimento (da studiare il codice, non solo leggere)

- **dotnet/eShop** (github.com/dotnet/eShop) – reference app ufficiale Microsoft: .NET Aspire, gRPC, RabbitMQ, Outbox, YARP/BFF, Keycloak-like identity, OpenTelemetry.
- **aspnetrun / mehmetozkaya – "Microservices Architecture and Step by Step Implementation on .NET"**: è esattamente lo stack dei tuoi appunti (Carter, Marten, MediatR, Mapster, MassTransit, FluentValidation, EF Core, Refit, YARP, Redis, SQLite, gRPC, docker-compose). Catalog/Basket/Discount/Ordering.
- **kgrzybek/modular-monolith-with-ddd** – DDD/Clean/Outbox/Inbox fatti benissimo (modular monolith: spesso il primo passo prima dei mss!).
- **"Microservices Patterns"** – Chris Richardson (microservices.io): il catalogo dei pattern.
- **"Building Microservices" (2ª ed.)** – Sam Newman.
- **".NET Microservices: Architecture for Containerized .NET Applications"** – e-book gratuito Microsoft.
- **"Designing Data-Intensive Applications"** – Martin Kleppmann (per capire davvero stateful, replica, consistenza, Kafka).
- Documentazione **MassTransit** (masstransit.io), **Marten** (martendb.io), **YARP**, **OpenTelemetry .NET**, **CloudNativePG**, **KEDA**.

---

### Riassunto mentale finale
- **Dentro** ogni servizio: VSA o Clean+DDD, CQRS con MediatR + behaviors, FluentValidation, Mapster, EF Core/Marten — *lo sai già da SubSnap*.
- **Tra** i servizi: gRPC/Refit per query sync con resilience handler, MassTransit + Outbox/Inbox per eventi, Saga per workflow, idempotenza sempre.
- **Davanti**: YARP gateway/BFF + Identity Provider.
- **Sotto**: servizi **stateless** scalabili all'infinito; tutto lo **stato** in DB/broker/cache/object storage, idealmente **managed**, altrimenti StatefulSet + Operator.
- **Attorno**: reti Docker a tier (`internal: true`), healthcheck, secrets, OpenTelemetry, CI/CD per servizio, GitOps.
