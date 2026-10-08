# Phase 4 — Restaurant management

Restaurant settings, categories, products, tables and product images now have persisted management workflows. SQL Server and an administrator from [Phase 3](phase-3-authentication.md) are required. This phase adds no schema changes: the two existing migrations already contain the required columns, constraints and rowversions.

## Use the application

1. Configure SQL Server, apply both migrations and provision an administrator using the Phase 2/3 instructions.
2. Restore and build the solution, then run `dotnet run --project src/ServeFlow.Web --launch-profile http`.
3. Sign in and open **Settings** to edit the restaurant name and time zone. Currency is displayed read-only because changing it would relabel existing product prices without conversion.
4. Open **Categories**, create a category, then open **Menu** to add its products. Edit prices, descriptions and availability; save the product before uploading an image.
5. Open **Tables** to create numbered tables and set their capacity. Administrators can edit/archive tables; administrators and waiters can change their status.

The default list view hides archived records. Categories and products support search; management lists use 20-item pages. Archive instead of physical deletion preserves existing order references. Restore using **Include archived**, then edit the record.

## Permissions and business rules

| Workflow | Authorized roles |
| --- | --- |
| Settings, category/product creation and edits, image uploads/removal | Administrator |
| Table creation, editing and archive | Administrator |
| Table list and status changes | Administrator, Waiter |
| Read a product image within the staff restaurant | Administrator, Waiter, KitchenStaff |

Both controllers and services check permissions. Services resolve the actor's active restaurant from stored Identity membership; a posted restaurant ID never controls ownership. Reads and writes include restaurant scope. IDs on edit actions bind explicitly from the route and must match the posted form ID. DTOs accept only editable fields; image keys, account ownership and SQL rowversions are not freely assignable.

Every edit includes its original base64 SQL rowversion. A stale save returns HTTP 409 with a reload instruction; it does not silently overwrite another employee's work. Mutations use retry-aware serializable transactions. Table numbers are unique within a restaurant; duplicate-number errors are sanitized.

An active category cannot be archived until its non-archived products are moved or archived. Restoring a product requires an active category. Archiving a product makes it unavailable. Menu edits leave historical order snapshots unchanged. Tables with nonterminal orders must remain occupied; archiving requires an available table with no active orders. Completed and cancelled orders are terminal for this check.

## Product image storage

Development defaults to `src/ServeFlow.Web/App_Data/product-images`, which is excluded from source control. Configure `Uploads:RootPath` as an **absolute directory outside wwwroot** in production. For example, set `Uploads__RootPath=/var/lib/serveflow/product-images` in the service environment and give the service account access to that directory. Back it up together with SQL Server. Multiple application instances must share the same durable storage directory; this implementation uses filesystem storage.

Accepted files are PNG or JPEG, at most 5 MiB of actual input, at most 4096 pixels per side and 8 megapixels, with no multiple frames. The service checks extensions, MIME types and signatures, then fully decodes and re-encodes pixels as PNG. It does not store the supplied filename or appended payload/metadata. Stored filenames are generated GUID keys; reads reject arbitrary paths. Processing is limited to two concurrent decodes per process, and uploads are rate limited to five per minute per signed-in account. Unsafe requests retain global antiforgery validation.

Images are served through a restaurant-scoped staff controller with `private, no-store`; they are not static public files. Anonymous QR-menu image access belongs to Phase 5. Unix directories/files created by this store use private permissions. The deployment operator must secure an already existing storage directory.

Image files and SQL transactions cannot commit atomically. New images are written immutably before the database reference changes; a second ownership/rowversion check protects that update. Replaced, removed and unreferenced files are retained. Automatic retention cleanup is not implemented; plan storage capacity and only remove unreferenced files with a reviewed backup/retention procedure. Retaining files avoids deleting a referenced image after an uncertain database commit.

The image decoder uses [SkiaSharp 4.153.1](https://www.nuget.org/packages/SkiaSharp/4.153.1) and [Linux native assets 4.153.1](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.153.1), pinned centrally and locked. Upload handling follows the bounded-input, generated-name and separate-storage practices described in [ASP.NET Core file upload documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).

## Validation and tests

```bash
dotnet restore ServeFlow.sln --locked-mode
dotnet build ServeFlow.sln --no-restore -c Release
dotnet test --solution ServeFlow.sln --no-build -c Release
```

Validation tests check prices, required fields, time zones, enum values and rowversion syntax. Image tests execute real native decoding and private filesystem writes, covering format/signature mismatches, truncated files, size/dimension limits, path rejection and payload removal. HTTP tests use real Identity cookies, policy checks, antiforgery and Razor views with a fixed management backend; they verify the web pipeline, not SQL transactions.

Seven new management service tests require `SERVEFLOW_TEST_SQLSERVER`. They create isolated `ServeFlowTests_<guid>` databases, apply migrations and drop only those generated databases afterward. Use a dedicated test server/account permitted to create and drop databases. The cases cover archive/restore rules, stale updates, cross-restaurant category ownership, waiter boundaries, active-order table rules, duplicate numbers and image reference concurrency. No InMemory or SQLite substitute proves these SQL Server behaviors. See [verification](verification.md) for executed results.

## Exact file map

New implementation and test files:

- `src/ServeFlow.Application/Management/ManagementContracts.cs`
- `src/ServeFlow.Application/Management/ManagementValidators.cs`
- `src/ServeFlow.Infrastructure/Management/ProductImageStore.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.Categories.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.Images.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.Menu.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.Settings.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.Tables.cs`
- `src/ServeFlow.Infrastructure/Management/RestaurantManagementService.cs`
- `src/ServeFlow.Web/Views/Restaurant/Index.cshtml`
- `src/ServeFlow.Web/Views/Categories/Form.cshtml`
- `src/ServeFlow.Web/Views/Categories/Index.cshtml`
- `src/ServeFlow.Web/Views/Menu/Form.cshtml`
- `src/ServeFlow.Web/Views/Menu/Index.cshtml`
- `src/ServeFlow.Web/Views/Tables/Form.cshtml`
- `src/ServeFlow.Web/Views/Tables/Index.cshtml`
- `src/ServeFlow.Web/Views/Tables/Status.cshtml`
- `src/ServeFlow.Web/Models/ManagementModels.cs`
- `src/ServeFlow.Web/Controllers/ManagementController.cs`
- `src/ServeFlow.Web/Controllers/RestaurantController.cs`
- `src/ServeFlow.Web/Controllers/CategoriesController.cs`
- `src/ServeFlow.Web/Controllers/MenuController.cs`
- `src/ServeFlow.Web/Controllers/TablesController.cs`
- `src/ServeFlow.Web/Controllers/ProductImagesController.cs`
- `src/ServeFlow.Web/Views/Shared/_ManagementFilter.cshtml`
- `src/ServeFlow.Web/Views/Shared/_ManagementPaging.cshtml`
- `tests/ServeFlow.UnitTests/ManagementValidationTests.cs`
- `tests/ServeFlow.UnitTests/ProductImageStoreTests.cs`
- `tests/ServeFlow.IntegrationTests/ManagementHttpTests.cs`
- `tests/ServeFlow.IntegrationTests/ManagementSqlServerTests.cs`

Updated existing files:

- `src/ServeFlow.Application/Accounts/AccountContracts.cs`: reusable conflict message while retaining the account-specific default.
- `src/ServeFlow.Infrastructure/DependencyInjection.cs`: management service and image-store/options registration.
- `src/ServeFlow.Infrastructure/ServeFlow.Infrastructure.csproj`: image decoder dependencies.
- `src/ServeFlow.Web/Program.cs`: per-account upload rate limiting.
- `src/ServeFlow.Web/Views/Shared/_Layout.cshtml`: role-aware management navigation.
- `src/ServeFlow.Web/wwwroot/css/site.css`: management layouts and image sizing.
- `tests/ServeFlow.IntegrationTests/AuthenticationTests.cs`: shared HTTP test helpers and optional management backend substitution.
- `Directory.Packages.props`: centrally pinned image dependencies.
- `src/ServeFlow.Application/packages.lock.json`: regenerated dependency lock.
- `src/ServeFlow.Domain/packages.lock.json`: regenerated dependency lock.
- `src/ServeFlow.Infrastructure/packages.lock.json`: regenerated dependency lock.
- `src/ServeFlow.Web/packages.lock.json`: regenerated dependency lock.
- `tests/ServeFlow.IntegrationTests/packages.lock.json`: regenerated dependency lock.
- `tests/ServeFlow.UnitTests/packages.lock.json`: regenerated dependency lock.
- `.gitignore`: excludes development upload storage.
- `README.md`, `docs/roadmap.md`, `docs/verification.md`: current milestone and results.
- `docs/phase-4-management.md`: this guide.

Next: Phase 5 implements QR table access, the public digital menu, customization/cart and transactional order submission with server-calculated totals and idempotency.
