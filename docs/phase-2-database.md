# Phase 2 — Database implementation

The entity model and initial SQL Server migration are implemented. Applying the migration and executing live SQL Server tests still require a configured SQL Server instance. No accounts, roles, restaurant records, or menu data are seeded at this stage. Phase 3 now implements authentication and safe administrator provisioning; see [the authentication guide](phase-3-authentication.md).

## Model decisions

- Domain contains ten framework-independent entities and four enums. `ApplicationUser` stays in Infrastructure and derives from `IdentityUser<Guid>`. The context derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`; standard Identity tables are included.
- Every restaurant-owned record has `RestaurantId`. Composite keys/foreign keys prevent cross-restaurant category, table, menu, order, payment, and employee assignments. Restaurant existence follows the required parent path for child entities, avoiding redundant direct foreign keys.
- `OrderItemOption` includes `MenuItemId` in both parent relationships. This prevents associating a valid option with the wrong menu item even when both belong to the same restaurant. Payment currency participates in its order foreign key, so mismatched currencies cannot be saved.
- All six monetary properties use SQL `decimal(18,2)`. The first release supports two-decimal currencies. Historical order lines store immutable name/price snapshots; menu edits do not alter those columns. Snapshot setters are `init`, and EF rejects updates after insertion. Direct SQL permissions must still be controlled; these are not database update-blocking triggers.
- Restaurant, table, category, menu item, option, order, and payment use SQL Server `rowversion`. Identity keeps its built-in concurrency stamp. Updates to order lines must also update/check the parent order version in the future ordering service.
- Business enums are stored as bounded strings with check constraints. Quantity is limited to 1–100 per order line; capacity to 1–100 per table. Prices and totals cannot be negative. Payments must be positive; only successful payments may have a `PaidAt` timestamp, and every successful payment must have one.
- Unique indexes cover table numbers per restaurant, non-null QR hashes, order numbers per restaurant, table-scoped idempotency keys, receipt hashes, selected options per line, and provider/payment references. Hashes use lowercase SHA-256 hexadecimal, length 64, and binary collation. Provider references are case-sensitive. QR hash is nullable until a staff member issues a code; the filtered unique index permits multiple unissued tables.
- Domain relationships use `NO ACTION` deletes. Archive menu/table/employee records and preserve history. Identity-owned claims, logins, tokens and role links retain their standard cascades. No restaurant/domain cascading paths are introduced.
- Historical order identity, timestamps and snapshots are immutable through normal EF writes. `DateTimeOffset.UtcNow` supplies initial timestamps; application services will validate client-independent timestamps and transitions.

The initial SQL creates **17 application/Identity tables**, plus `__EFMigrationsHistory`. There are no raw card fields, secrets, predictable QR tokens, or default password seeds. Currency codes are three uppercase letters; application validation will restrict them to supported currencies. Time-zone names are bounded storage strings; application validation will resolve them before use.

Database constraints complement application behavior. They cannot by themselves prove a cart has at least one item, derive totals across rows, reject unavailable products at checkout, authorize users, compare idempotency request fingerprints, or validate an actor's order status transitions. Those services arrive in Phases 4–6. No public order endpoint exists yet.

## Source files

All paths below are relative to `/home/pc/Documents/restaurant-ordering-project`.

```text
src/ServeFlow.Domain/Entities/Restaurant.cs
src/ServeFlow.Domain/Entities/RestaurantTable.cs
src/ServeFlow.Domain/Entities/MenuCategory.cs
src/ServeFlow.Domain/Entities/MenuItem.cs
src/ServeFlow.Domain/Entities/MenuItemOption.cs
src/ServeFlow.Domain/Entities/Order.cs
src/ServeFlow.Domain/Entities/OrderItem.cs
src/ServeFlow.Domain/Entities/OrderItemOption.cs
src/ServeFlow.Domain/Entities/Payment.cs
src/ServeFlow.Domain/Entities/OrderStatusHistory.cs
src/ServeFlow.Domain/Enums/TableStatus.cs
src/ServeFlow.Domain/Enums/OrderStatus.cs
src/ServeFlow.Domain/Enums/PaymentStatus.cs
src/ServeFlow.Domain/Enums/PaymentMethod.cs
src/ServeFlow.Infrastructure/Identity/ApplicationUser.cs
src/ServeFlow.Infrastructure/Persistence/ServeFlowDbContext.cs
src/ServeFlow.Infrastructure/Persistence/ServeFlowDbContextFactory.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/MappingExtensions.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/RestaurantConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/RestaurantTableConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/MenuCategoryConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/MenuItemConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/MenuItemOptionConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/OrderConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/OrderItemConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/OrderItemOptionConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/OrderStatusHistoryConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/20261008183530_InitialSchema.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/20261008183530_InitialSchema.Designer.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/ServeFlowDbContextModelSnapshot.cs
tests/ServeFlow.UnitTests/PersistenceModelTests.cs
tests/ServeFlow.IntegrationTests/SqlServerDatabaseTests.cs
dotnet-tools.json
docs/database/InitialSchema.sql
```

## Restore, inspect and apply

Phase 3 adds a second migration for unique normalized account emails; the complete current script is `docs/database/ServeFlowSchema.sql`. The local tool manifest pins `dotnet-ef` 10.0.12 to match the existing EF packages. From the repository root:

```bash
dotnet restore ServeFlow.sln --locked-mode
dotnet tool restore
dotnet build ServeFlow.sln -c Release --no-restore
dotnet ef migrations list --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build --no-connect
dotnet ef migrations has-pending-model-changes --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build
```

For development, configure the actual SQL Server host/login through user secrets using the command in [Phase 1 setup](phase-1-setup.md). Then:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build
```

`ServeFlowDbContextFactory` reads Web's user-secrets ID only in Development and allows environment variables to override it. `ConnectionStrings__ServeFlow` works in all environments. `DOTNET_ENVIRONMENT`, if set, takes precedence over `ASPNETCORE_ENVIRONMENT`; avoid contradictory values. The factory does not read `appsettings` files or start the Web host. Its no-secret fallback is for offline model generation, not a provisioned database. Configured production connections use the same encryption and certificate checks as runtime connections.

Migration application needs an identity authorized to create/alter the target schema. Runtime credentials should not have those privileges. Review [InitialSchema.sql](database/InitialSchema.sql) before deployment. It is idempotent against EF's migration history and includes transaction boundaries. Do not run it against an unmanaged existing schema that merely happens to share table names.

Regenerate the script after future migrations:

```bash
dotnet ef migrations script --idempotent --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build --output docs/database/InitialSchema.sql
```

The application never invokes `EnsureCreated` or automatically migrates on startup. Use a separate deployment migration step. Avoid `EnsureCreated` for this application: it bypasses migration history.

## Real SQL Server integration tests

Set `SERVEFLOW_TEST_SQLSERVER` through your local environment or test runner secrets to a connection for a **dedicated test server**. Do not commit its value. The test login needs permission to create/drop test databases. Each test replaces the supplied database name with a new `ServeFlowTests_<random-guid>` name, migrates it, and deletes that generated database during teardown. A process crash can leave a test database behind; clean up only that prefix on your test server.

```bash
dotnet test --solution ServeFlow.sln --no-build -c Release
```

Without the variable, all eleven live SQL Server cases are explicitly skipped. With it, they exercise migration application, composite ownership foreign keys, real rowversion concurrency, historical price preservation, unique idempotency enforcement, quantity/price check constraints, option membership, payment state/currency constraints, collection filtering, and restricted deletes. If the variable is set but the server is unreachable or credentials lack permissions, the tests fail rather than silently skip.

Unit-level metadata checks execute without a database and verify precision, shadow-property absence, concurrency mapping, constrained relationships, delete behavior, immutable snapshot mapping, indexes/check constraints, migration snapshot parity, and idempotent SQL generation. These do not substitute for executing the SQL Server cases.

## Relationship implementation reference

The ownership checks use EF Core's composite foreign/principal keys as described in [Microsoft's relationship documentation](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys). See [the architecture diagram](architecture.md) for the conceptual ER overview; this guide explains the additional physical keys.
