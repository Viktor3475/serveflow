# Phase 6 — Kitchen workflows and real-time updates

ServeFlow now has a dedicated kitchen screen, a shared staff order queue, validated lifecycle changes and live customer receipt updates. No new schema or migration is required. Complete [Phase 5](phase-5-ordering.md) first so you have staff, a menu, QR table access and orders.

## Try the workflow

1. Run the configured application and sign in as administrator or kitchen staff. Open **Kitchen**. Administrators can also use **Live orders**; waiters use **Live orders**.
2. Scan a table QR code in a separate customer browser and submit an order. Its Pending card appears automatically in the staff queue.
3. Accept it, start preparation, then mark it Ready. On the customer receipt, the status refreshes automatically.
4. Sign in as a waiter, open **Live orders**, mark it Served, then Completed. The queue removes terminal orders. The table becomes Available only when no other nonterminal order remains at that table.
5. For cancellation, use **Cancel** and enter a reason. Cancelled orders leave the active queue but their receipts/history remain.

Queue cards show snapshot product/option names, quantities, item instructions, order notes, table, time and status. Kitchen-only staff see Pending through Ready. Administrators and waiters see all nonterminal restaurant orders, with buttons determined by their role. The oldest 100 orders are returned with the actual total count; process that backlog to reveal newer entries. Theme and mobile layout use the existing local Bootstrap/CSS assets.

## Lifecycle rules

| Current status | Next status | Roles |
| --- | --- | --- |
| Pending | Accepted | Administrator, KitchenStaff |
| Accepted | Preparing | Administrator, KitchenStaff |
| Preparing | Ready | Administrator, KitchenStaff |
| Ready | Served | Administrator, Waiter |
| Served | Completed | Administrator, Waiter |
| Pending | Cancelled | Administrator, KitchenStaff, Waiter |
| Accepted or Preparing | Cancelled | Administrator, KitchenStaff |
| Ready | Cancelled | Administrator |
| Completed or Cancelled | None | Terminal |

Skipping steps, repeating the same status and reversing transitions are rejected. A cancellation requires a nonblank reason of at most 500 characters. Every write carries the original SQL rowversion; a stale command returns 409 and the client reloads the queue. Status command JSON is bounded to 4 KiB. UI buttons are a convenience; the service rechecks current role, active membership, restaurant ownership, version and transition.

Status/history/table changes commit together in a retry-aware serializable SQL Server transaction. The service acquires a table update lock before loading/updating the order, matching order creation's lock order. The history records previous/new statuses, timestamp, trusted staff actor and reason. Terminal transitions release a table only if no other active order exists. Completed represents the fulfillment lifecycle; it does not create a payment or imply successful collection. Payment analytics must use successful Payment records in Phase 7.

## SignalR access and delivery

`/hubs/orders` is a shared transport endpoint with these methods:

- `SubscribeStaff()`: requires the staff policy and stored active membership. The restaurant comes from the database, and the cookie's security stamp must match the current account.
- `SubscribeReceipt(orderId, receiptToken)`: requires the same private receipt capability as the receipt HTTP API. The raw token is sent in the hub invocation, not the URL.
- `Renew()`: rechecks the connection's current subscription and renews its two-minute access lease. Clients call it every 30 seconds.

Each connection has one server-owned subscription. There is no caller-supplied restaurant/group name or general broadcast method. Receipt subscriptions hold only the token hash. Disconnects remove subscriptions. A notifier rechecks current membership/security stamp or receipt access before every targeted delivery and aborts/removes revoked or expired subscriptions. It emits an empty `OrdersChanged` hint; no names, totals, IDs or capabilities appear in the event. Clients then read their authorized HTTP resource. Changing a role/archive status through the existing account service rotates the stamp, so old staff subscriptions cannot continue receiving hints.

Order creation and successful status changes trigger notifications only after commit. Idempotent order replays do not publish another creation hint. Live notification delivery is best effort, capped at five seconds and isolated from the committed operation's HTTP result. A missed event does not become a failed order submission. There is no durable event outbox in this phase. Every visible client also refreshes through HTTP every 15 seconds, and resynchronizes after joining, reconnecting or returning to the tab. This repairs missed notifications and process restarts.

The browser client retries initial connection failures, uses automatic reconnect, resubscribes on the new connection and shows its connection state. If the transport library fails to load, the same HTTP polling remains available. Updates use DOM text nodes rather than interpolating submitted notes into HTML. Manual refresh buttons remain available.

Cross-origin hub requests with an Origin header are rejected unless scheme/authority match the current request. Behind a reverse proxy, preserve the external host/scheme through trusted proxy configuration; the Phase 8 deployment must configure this correctly. No permissive CORS policy is added. Hub invocations are capped at 20 per minute per connection, receive messages at 4 KiB, and allow one parallel invocation. Hub transport requests retain the public read IP limit; staff queue reads/writes are limited to 120/60 per minute per account. Unsafe MVC/API requests retain antiforgery validation. Customer and operations responses use private `no-store` headers.

Connection tracking and SignalR delivery are local to one host. This phase supports a single application instance. A multi-instance deployment requires a distributed delivery/backplane design as well as shared cookie keys/storage; adding Redis alone does not distribute this application's subscription registry. Polling continues to recover state across instances, but it is not cross-instance push.

The server uses SignalR from the ASP.NET Core framework. The browser bundle is the official `@microsoft/signalr` **10.0.11** release verified through npm; it is served locally with its MIT license, source map and package-integrity/SHA-256 provenance. The transport tests use [Microsoft.AspNetCore.SignalR.Client 10.0.12](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR.Client/10.0.12). Reconnect and authorization behavior follows the official [JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0) and [SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0) documentation.

## API

| Route | Access and behavior |
| --- | --- |
| `GET /Kitchen` | Administrator/KitchenStaff; dedicated kitchen screen |
| `GET /Operations` | Administrator/Waiter/KitchenStaff; live staff queue |
| `GET /api/operations/queue` | Stored active staff; scoped active orders and role-allowed actions |
| `POST /api/operations/orders/{id}/status` | Staff cookie + CSRF; body `{ status, version, reason }` |
| `/hubs/orders` | Anonymous transport; independently authorized subscriptions |

The numeric status values are the existing enum: Pending 0, Accepted 1, Preparing 2, Ready 3, Served 4, Completed 5, Cancelled 6. Use only the returned `actions` values and original `version` when posting. Success returns `{ saved: true }`; invalid transitions/fields return 400, stale writes 409, and rate limits 429. Ownership/inactive-account failures forbid access. Staff cookie challenges may redirect to login or access-denied.

## Verification

```bash
dotnet restore ServeFlow.sln --locked-mode
dotnet build ServeFlow.sln --no-restore -c Release
dotnet test --solution ServeFlow.sln --no-build -c Release
```

Unit tests verify role/state transitions and terminal behavior. HTTP tests use real Identity cookies, Razor, CSRF and rate policies with substituted workflow persistence. The SignalR tests use actual .NET hub clients and TestServer Long Polling, not direct calls to a hub class. They cover anonymous staff denial, invalid receipt denial, receipt-specific delivery, reconnect/resubscription, restaurant isolation, stamp revocation, publication failure handling and invocation limits.

Five new opt-in SQL tests cover a full lifecycle/history, last-active-order table release, cancellation reasons, concurrent acceptance, role separation and cross-restaurant/archive rejection. They apply existing migrations to generated test databases and drop only those generated databases afterward. Set `SERVEFLOW_TEST_SQLSERVER` using the Phase 2 guide. No SQL test connection is configured in this workspace, so these tests are explicit skips. Transport tests substitute the workflow service and do not establish SQL transaction behavior. Browser WebSocket/mobile visual review, production deployment and a full persisted multi-device workflow remain unexecuted. See [verification](verification.md) for final counts and actual checks.

## Exact file map

New files:

- `src/ServeFlow.Application/Operations/OperationsContracts.cs`
- `src/ServeFlow.Infrastructure/Operations/OperationsService.cs`
- `src/ServeFlow.Web/Realtime/OrderConnections.cs`
- `src/ServeFlow.Web/Realtime/OrdersHub.cs`
- `src/ServeFlow.Web/Realtime/OrdersHubRateFilter.cs`
- `src/ServeFlow.Web/Realtime/SignalROrderNotifier.cs`
- `src/ServeFlow.Web/Views/Operations/Index.cshtml`
- `src/ServeFlow.Web/wwwroot/lib/signalr/LICENSE.txt`
- `src/ServeFlow.Web/wwwroot/lib/signalr/provenance.json`
- `src/ServeFlow.Web/wwwroot/lib/signalr/signalr.min.js`
- `src/ServeFlow.Web/wwwroot/lib/signalr/signalr.min.js.map`
- `src/ServeFlow.Web/Controllers/OperationsController.cs`
- `src/ServeFlow.Web/Controllers/KitchenController.cs`
- `src/ServeFlow.Web/wwwroot/js/order-live.js`
- `src/ServeFlow.Web/wwwroot/js/operations.js`
- `tests/ServeFlow.UnitTests/OrderTransitionTests.cs`
- `tests/ServeFlow.IntegrationTests/OperationsHttpTests.cs`
- `tests/ServeFlow.IntegrationTests/OperationsSqlServerTests.cs`

Updated files:

- `src/ServeFlow.Infrastructure/DependencyInjection.cs`: workflow service and a no-op notifier for infrastructure-only hosts/tests.
- `src/ServeFlow.Infrastructure/Ordering/OrderingService.cs`: creation notification after a successful new-order commit.
- `src/ServeFlow.Web/Program.cs`: SignalR/notifier registration, hub limits/mapping, origin checks and operations rate/privacy policies.
- `src/ServeFlow.Web/Views/Shared/_Layout.cshtml`: kitchen/live-order navigation by role.
- `src/ServeFlow.Web/Views/Dining/Receipt.cshtml`: local transport scripts and connection indicator.
- `src/ServeFlow.Web/wwwroot/js/dining-receipt.js`: authorized live receipt subscription and automatic resynchronization.
- `src/ServeFlow.Web/wwwroot/css/site.css`: readable status badges and order-note styling.
- `Directory.Packages.props`: pinned .NET SignalR test client.
- `tests/ServeFlow.IntegrationTests/ServeFlow.IntegrationTests.csproj`: transport test dependency.
- `tests/ServeFlow.IntegrationTests/AuthenticationTests.cs`: workflow backend injection for HTTP/transport tests.

- `src/ServeFlow.Application/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `src/ServeFlow.Domain/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `src/ServeFlow.Infrastructure/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `src/ServeFlow.Web/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `tests/ServeFlow.IntegrationTests/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `tests/ServeFlow.UnitTests/packages.lock.json`: dependency lock restored with the new test-client configuration.
- `README.md`, `docs/roadmap.md`, `docs/verification.md`: milestone, next phase and actual verification.
- `docs/phase-6-realtime.md`: this guide.

Next is Phase 7: administration analytics, date/time-zone filters, successful-payment aggregation and the broader UI refinement.
