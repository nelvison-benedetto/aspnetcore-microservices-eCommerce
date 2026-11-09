# Architettura a Microservices – eCommerceSolution

Documento basato sul codice reale di:
- `eCommerceSolution.OrdersService` (contiene anche **ApiGateway** e il **docker-compose.yml** che avvia TUTTO)
- `eCommerceSolution.ProductsService`
- `eCommerceSolution.UsersService`
- cartelle di init: `mongodb-init/`, `mysql-init/`, `postgres-init/`

---

## 1. Risposte veloci alle tue domande

| Domanda | Risposta breve |
|---|---|
| Ho un container RabbitMQ che parla con i miei 3 mss? | **No, solo con 2.** Products (publisher) e Orders (consumer). Users non usa RabbitMQ (non ha nemmeno le env `RabbitMQ_*` nel compose). |
| E lo stesso per Redis? | **Redis è usato solo da Orders.** Products e Users non hanno `AddStackExchangeRedisCache` né le env `REDIS_*`. |
| Quindi la cache Redis è condivisa tra i 3 mss? | **Tecnicamente potrebbe esserlo** (è un unico container sulla rete `ecommerce-network`, chiunque su quella rete può collegarsi a `redis:6379`), **ma nel tuo codice la usa solo Orders**, come cache dei dati che Orders prende da Users e Products (`user:{id}`, `product:{id}`). |
| Sto ancora usando HTTP tra mss? | **Sì.** Orders chiama Users e Products via `HttpClient` (passando dal Gateway). RabbitMQ lo usi solo per gli **eventi** Products → Orders (delete / update). |
| Dovrei usare solo RabbitMQ/Kafka? | **Non necessariamente.** È normale e corretto avere un mix: HTTP per le *query* sincrone ("dammi l'utente X adesso"), messaggi per gli *eventi* ("il prodotto X è stato cancellato"). Vedi sezione 6. |

---

## 2. La mappa dei container

Tutto viene avviato da **un solo** `docker-compose.yml` (quello in `eCommerceSolution.OrdersService`). Avvia **10 container**:

| Container (nome = hostname DNS interno) | Immagine | Porta host → porta container | Reti |
|---|---|---|---|
| `apigateway` | build da `ApiGateway/Dockerfile` (Ocelot) | `4000 → 8080` | ecommerce-network |
| `ordersmicroservice.api` | build da `OrdersMicroservice.API/Dockerfile` | `7000 → 8080` | ecommerce-network, orders-mongodb-network |
| `products-microservice` | `products-microservice:latest` (immagine già buildata) | `6001 → 8080` | ecommerce-network, products-mysql-network |
| `users-microservice` | `users-microservice:latest` (immagine già buildata) | `5000 → 9090` | ecommerce-network, users-postgres-network |
| `mongodb-container` | `mongo:latest` | `27017 → 27017` | orders-mongodb-network |
| `mysql-container` | `mysql:8.0` | `3307 → 3306` | products-mysql-network |
| `postgres-container` | `postgres:13` | `5433 → 5432` | users-postgres-network |
| `redis` | `redis:latest` | `6379 → 6379` | ecommerce-network |
| `rabbitmq` | `rabbitmq:3.13-management` | `5672 → 5672` (AMQP), `15672 → 15672` (UI web) | ecommerce-network |

> Nota: Products e Users nel compose usano `image:` e non `build:` → devi prima buildare le loro immagini dalle rispettive soluzioni (`docker build -t products-microservice:latest -f ProductsService.API/Dockerfile .` ecc.). Orders e ApiGateway invece vengono buildati dal compose.

### Diagramma

```
   Browser / Angular (localhost:4200)
              │  http://localhost:4000
              ▼
╔══════════════════════════════ ecommerce-network ══════════════════════════════╗
║                                                                               ║
║                         ┌──────────────────────┐                              ║
║                         │      apigateway      │  Ocelot :8080                ║
║                         └──────────┬───────────┘                              ║
║              ┌─────────────────────┼─────────────────────┐                    ║
║              ▼ HTTP                ▼ HTTP                ▼ HTTP               ║
║   ┌────────────────────┐  ┌────────────────────┐  ┌────────────────────┐      ║
║   │ordersmicroservice  │  │products-microservice│ │ users-microservice │      ║
║   │      .api :8080    │  │       :8080        │  │       :9090        │      ║
║   └──┬───────┬──────▲──┘  └─────────┬──────────┘  └─────────┬──────────┘      ║
║      │       │      │ consume       │ publish               │                 ║
║      │       │   ┌──┴───────────────▼──┐                    │                 ║
║      │       │   │ rabbitmq :5672      │                    │                 ║
║      │       │   │ product.exchange    │                    │                 ║
║      │       │   └─────────────────────┘                    │                 ║
║      │       ▼                                              │                 ║
║      │   ┌────────┐   (Orders → apigateway → users/products │                 ║
║      │   │ redis  │    per leggere utenti e prodotti)       │                 ║
║      │   │ :6379  │                                         │                 ║
║      │   └────────┘                                         │                 ║
╚══════╪═══════════════════════════╪══════════════════════════╪═════════════════╝
       │ orders-mongodb-network    │ products-mysql-network   │ users-postgres-network
       ▼                           ▼                          ▼
 ┌──────────────┐          ┌──────────────┐           ┌────────────────┐
 │mongodb-      │          │mysql-        │           │postgres-       │
 │container     │          │container     │           │container       │
 └──────────────┘          └──────────────┘           └────────────────┘
```

Versione semplificata (chi parla con chi):

```
Frontend ──HTTP──► apigateway ──HTTP──► orders / products / users

orders ──HTTP (via apigateway)──► users      (GET /gateway/users/{id})
orders ──HTTP (via apigateway)──► products   (GET /gateway/products/search/product-id/{id})
orders ──TCP──► redis        (cache user:{id}, product:{id})
orders ──TCP──► mongodb      (database ordini)
orders ◄─AMQP── rabbitmq ◄─AMQP── products   (eventi product.delete / product.update)
products ──TCP──► mysql      (database prodotti)
users    ──TCP──► postgres   (database utenti)
```

---

## 3. Come si "trovano" i container: reti Docker e DNS

Questo è il punto chiave che ti manca.

### 3.1 Ogni container è un mini-computer separato
Ogni container ha il suo IP, il suo `localhost`, il suo filesystem. **`localhost` dentro un container = il container stesso**, non il tuo PC e non gli altri container. Ecco perché negli `appsettings.json` hai `localhost` (valido quando lanci da Visual Studio sul PC) ma nel compose lo sovrascrivi con `rabbitmq`, `redis`, `mongodb-container`, ecc.

### 3.2 Il nome del servizio nel compose È l'hostname
Docker Compose crea un DNS interno: sulle reti comuni, ogni container è raggiungibile col **nome del servizio**:

- `RabbitMQ_HostName=rabbitmq` → il container `rabbitmq`
- `REDIS_HOST=redis` → il container `redis`
- `MONGODB_HOST=mongodb-container`
- `MYSQL_HOST=mysql-container`
- `POSTGRES_HOST=postgres-container`
- In `ocelot.json`: `"Host": "ordersmicroservice.api"`, `"products-microservice"`, `"users-microservice"`

### 3.3 Le reti isolano chi può parlare con chi
Hai 4 reti bridge:

| Rete | Chi c'è dentro | Scopo |
|---|---|---|
| `ecommerce-network` | gateway, orders, products, users, redis, rabbitmq | la rete "condivisa" dei servizi |
| `orders-mongodb-network` | orders, mongodb | solo Orders vede il suo DB |
| `products-mysql-network` | products, mysql | solo Products vede il suo DB |
| `users-postgres-network` | users, postgres | solo Users vede il suo DB |

🔥Conseguenza: **Products non può raggiungere MongoDB**, Users non può raggiungere MySQL, ecc. È il pattern **Database-per-Service** applicato anche a livello di rete. I mss che stanno su due reti fanno da "ponte" tra la rete comune e il loro DB privato.

### 3.4 Porte: interne vs pubblicate
`"7000:8080"` significa **porta del PC : porta del container**.

- Tra container si usa **sempre la porta interna** (`ordersmicroservice.api:8080`, `users-microservice:9090`, `rabbitmq:5672`).
- La porta di sinistra (7000, 6001, 5000, 4000, 3307, 5433…) serve solo a **te dal PC** (Swagger, DBeaver, UI RabbitMQ su `http://localhost:15672`, ecc.).
- In produzione di solito pubblicheresti **solo** il gateway (4000) e lasceresti tutto il resto non esposto.

Users ascolta su **9090** perché in `eCommerce.API/appsettings.json` c'è `Kestrel:Endpoints:Http:Url = http://0.0.0.0:9090`, che ha la precedenza su `ASPNETCORE_HTTP_PORTS=8080`. Per questo nel compose c'è `5000:9090` e in ocelot `users-microservice:9090`.

### 3.5 Volumi
- `../mongodb-init`, `../mysql-init`, `../postgres-init` montati in `/docker-entrypoint-initdb.d` → gli script vengono eseguiti **solo al primo avvio** (quando il DB è vuoto) per creare tabelle/dati.
- `c:/microservices/redis-cache:/data` → i dati di Redis sopravvivono al riavvio del container.
- I DB invece **non** hanno un volume dati: se fai `docker compose down` e rimuovi i container, perdi i dati (poi vengono ricreati dagli script di init).

---

## 4. I tre microservizi: cosa possiede ciascuno

| | Orders | Products | Users |
|---|---|---|---|
| Database | MongoDB (`OrdersDatabase`) | MySQL (`ecommerceproductsdatabase`) – EF Core | PostgreSQL (`eCommerceUsers`) – Dapper |
| Redis | ✅ `IDistributedCache` | ❌ | ❌ |
| RabbitMQ | ✅ **Consumer** (2 code) | ✅ **Publisher** | ❌ |
| Chiama altri mss via HTTP | ✅ Users e Products (via gateway) + Polly | ❌ | ❌ |
| Viene chiamato da | gateway | gateway (anche per conto di Orders) | gateway (anche per conto di Orders) |

Products e Users sono **autonomi**: non dipendono da nessun altro microservizio. Orders è quello "che compone" i dati (un ordine contiene `UserID` e `ProductID`, ma la risposta deve mostrare nome utente, email, nome prodotto, categoria).

---

## 5. I flussi reali, passo per passo

### 5.1 Il frontend crea un ordine — `POST /gateway/Orders/`

1. Angular → `http://localhost:4000/gateway/Orders/` → **apigateway**.
2. Ocelot (`ocelot.json`) riscrive in `http://ordersmicroservice.api:8080/api/Orders`.
3. `OrdersService.AddOrder` per ogni item chiama `ProductsMicroserviceClient.GetProductByProductID`:
   - legge Redis `product:{id}` → se c'è, usa quello (**nessuna chiamata HTTP**);
   - altrimenti `GET http://apigateway:8080/gateway/products/search/product-id/{id}` → il gateway inoltra a `products-microservice:8080` → MySQL;
   - salva il risultato in Redis per 300 s.
   - Polly: **Fallback** + **Bulkhead** (max 2 richieste parallele, 40 in coda).
4. Poi `UsersMicroserviceClient.GetUserByUserID`: stessa logica con chiave `user:{id}` → `apigateway` → `users-microservice:9090` → Postgres. Polly: Retry + CircuitBreaker + Timeout (combinati).
5. Salva l'ordine in MongoDB, arricchisce la risposta con AutoMapper, e la restituisce.

Nota: nel compose `UsersMicroserviceName=apigateway` e `ProductsMicroserviceName=apigateway` → **Orders non chiama direttamente gli altri mss, passa dal gateway** (per questo i path iniziano con `/gateway/...`). È una scelta del corso: comoda perché eredita retry/QoS/rate-limit di Ocelot, ma aggiunge un hop. Molti sistemi fanno chiamare i mss tra loro direttamente sulla rete interna e usano il gateway solo per il traffico esterno.

### 5.2 Un prodotto viene cancellato — evento via RabbitMQ

1. Frontend → gateway → `DELETE products-microservice:8080/api/products/{id}`.
2. `ProductsService.DeleteProduct` cancella da MySQL e poi `RabbitMQPublisher.PublishAsync` pubblica su exchange **`product.exchange`** (tipo **headers**) con header `event=product.delete`, `RowCount=1` e body `{ ProductID, ProductName }`.
3. RabbitMQ confronta gli header con i binding: la coda `orders.product.delete.queue` è legata con `x-match=all, event=product.delete, RowCount=1` → il messaggio finisce lì.
4. In Orders, `RabbitMQProductDeletionHostedService` (BackgroundService sempre attivo) ha avviato `RabbitMQProductDeletionConsumer`, che riceve il messaggio e fa `Redis.Remove("product:{id}")`.

### 5.3 Un prodotto viene modificato

Uguale, con header `event=product.update`; il body è l'intero `Product` aggiornato. La coda `orders.product.update.name.queue` lo riceve e il consumer **sovrascrive** `product:{id}` in Redis con i dati nuovi (300 s).

### 5.4 Il ruolo di RabbitMQ nel tuo sistema, in una frase
RabbitMQ serve a **tenere coerente la cache Redis di Orders** con i cambiamenti avvenuti in Products, **senza** che Products debba conoscere Orders o chiamarlo via HTTP. Products pubblica e "non sa" chi ascolta: domani potresti aggiungere un mss Notifications che si lega allo stesso exchange senza toccare Products.

Dettagli robusti che hai già:
- Exchange e code `durable: true`.
- Il consumer apre la connessione in `ConsumeAsync` (non nel costruttore) e l'HostedService riprova ogni 5 s → Orders parte anche se RabbitMQ non è ancora pronto (nel compose non c'è `depends_on: rabbitmq`).
- Il publisher apre la connessione in modo lazy e usa un `SemaphoreSlim` perché `IChannel` non è thread-safe.

---

## 6. HTTP vs RabbitMQ/Kafka: devi eliminare gli HttpClient?

**No, non "dovresti" usare solo messaggi.** Le due cose risolvono problemi diversi:

| Comunicazione **sincrona** (HTTP/gRPC) | Comunicazione **asincrona** (RabbitMQ/Kafka) |
|---|---|
| "Mi serve una risposta **adesso** per continuare" | "È successo qualcosa, chi è interessato reagisca" |
| Query: *dammi l'utente X* | Eventi: *ProductDeleted*, *OrderPlaced* |
| Accoppiamento temporale: se Users è giù, Orders soffre (→ Polly, fallback, cache) | Disaccoppiato: se Orders è giù, i messaggi aspettano in coda (code durable) |
| Semplice da ragionare | Consistenza **eventuale**, più complesso da debuggare |

Nel tuo caso:
- **Verificare che il ProductID/UserID esista quando crei un ordine** → è naturalmente sincrono, HTTP va bene.
- **Invalidare/aggiornare la cache quando un prodotto cambia** → asincrono, RabbitMQ è la scelta giusta (ed è quello che hai fatto).

### Se volessi andare "più event-driven"
Il passo successivo tipico non è "sostituire HTTP con RabbitMQ request/reply", ma:
1. Orders mantiene una **copia locale** (read model) dei dati che gli servono di Users/Products (nome, email, nome prodotto, categoria), alimentata dagli eventi `UserCreated/UserUpdated`, `ProductCreated/ProductUpdated/ProductDeleted`.
2. Così Orders non deve più chiamare nessuno per leggere → niente HttpClient, niente circuit breaker per quelle letture.
3. Costo: dati duplicati e consistenza eventuale (per qualche ms/secondo Orders può vedere il nome vecchio).

Oggi Redis fa già una versione "leggera" di questo per i prodotti (cache aggiornata dagli eventi). Per gli utenti invece Redis scade dopo 3 minuti e poi richiama HTTP, perché Users non pubblica eventi.

**Kafka vs RabbitMQ**: RabbitMQ è un message broker (code, routing con exchange, il messaggio sparisce dopo l'ack). Kafka è un log distribuito (gli eventi restano salvati, si possono rileggere, throughput altissimo). Per questo progetto RabbitMQ è più che adeguato.

---

## 7. Cose che ho notato nel codice/config (da controllare)

1. **Typo nel compose**: `apigateway.build.dockerfile: ApiGatway/Dockerfile` – la cartella si chiama `ApiGateway`. Il build del gateway dal compose fallirà finché non correggi in `ApiGateway/Dockerfile`.
2. **Cache del gateway**: `ocelot.json` usa `FileCacheOptions` sulla rotta Orders, ma in `ApiGateway/Program.cs` registri solo `.AddOcelot().AddPolly()`. Per far funzionare la cache di Ocelot serve anche il provider di cache (es. pacchetto `Ocelot.Cache.CacheManager` + `.AddCacheManager(...)`); senza, quell'opzione probabilmente non ha effetto.
3. **Rate limit su `/gateway/Products/`**: max 3 richieste al minuto. Orders non ne risente, perché usa `/gateway/products/search/product-id/{id}`, una rotta diversa senza rate limit. Il frontend che lista i prodotti invece riceverà 429 dalla 4ª richiesta in un minuto (probabilmente voluto per la demo).
4. **`autoAck: true`** nei consumer: il messaggio è considerato consegnato appena arriva; se la scrittura su Redis fallisce, il messaggio è perso. Per la produzione: `autoAck: false` + `BasicAckAsync` dopo l'elaborazione (ed eventualmente una dead-letter queue).
5. **Nome exchange incoerente nei default**: nel compose è `product.exchange` (per entrambi, quindi funziona), ma negli `appsettings.json`/Dockerfile di default è `products.exchange`. Se avvii un servizio dal compose e l'altro da Visual Studio, pubblicano/ascoltano su exchange diversi e i messaggi non arrivano.
6. **Users nel compose**: `ASPNETCORE_HTTP_ENVIRONMENT=Development` non è una variabile riconosciuta (quella giusta è `ASPNETCORE_ENVIRONMENT`).
7. Le porte dei DB (27017, 3307, 5433), di Redis e di RabbitMQ sono pubblicate sull'host: comodo in sviluppo, da togliere in produzione.

---

## 8. Riassunto mentale

- **Un container = un processo isolato**. Si parlano solo se condividono una **rete Docker**, usando il **nome del servizio** come hostname e la **porta interna**.
- **apigateway** è l'unica porta d'ingresso per il frontend; instrada verso i 3 mss.
- **Ogni mss ha il suo DB su una rete privata** (database-per-service).
- **Redis e RabbitMQ sono container "infrastrutturali" sulla rete comune**: in teoria accessibili da tutti, nella pratica Redis lo usa solo Orders e RabbitMQ lo usano Products (pubblica) e Orders (consuma).
- **HTTP** = Orders che chiede dati a Users/Products quando gli servono (protetto da Polly + cache Redis).
- **RabbitMQ** = Products che annuncia "prodotto cambiato/cancellato" e Orders che aggiorna la propria cache. Le due modalità convivono, ed è normale così.
