# Phase 1 — Architecture and setup

## Versions and packages

Target **.NET 10 LTS** with the latest stable .NET 10 SDK. The supplied machine has 10.0.112; `global.json` uses a minimum 10.0.100 and `latestFeature` to allow stable updates. Verify current SDKs on the [official download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Preview SDKs are disallowed.

These versions were checked against the official NuGet catalog during setup and are pinned centrally in `Directory.Packages.props`; lock files pin the transitive graph.

| Package | Version | Project / purpose |
| --- | --- | --- |
| [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/10.0.12) | 10.0.12 | Infrastructure / SQL Server provider |
| [Microsoft.AspNetCore.Identity.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore/10.0.12) | 10.0.12 | Infrastructure / Identity storage in Phase 2 |
| [Microsoft.EntityFrameworkCore.Design](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.12) | 10.0.12 | Web / EF tooling, private dependency |
| [FluentValidation.DependencyInjectionExtensions](https://www.nuget.org/packages/FluentValidation.DependencyInjectionExtensions/12.1.1) | 12.1.1 | Application / async validators and discovery |
| [Serilog.AspNetCore](https://www.nuget.org/packages/Serilog.AspNetCore/10.0.0) | 10.0.0 | Web / structured console logging |
| [Microsoft.AspNetCore.Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/10.0.12) | 10.0.12 | IntegrationTests / in-process HTTP tests |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.1) | 18.10.1 | Test projects / VSTest support |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3/4.0.1) | 4.0.1 | Test projects / maintained xUnit v3 framework |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.0) | 4.0.0 | Test projects / discovery and execution |

MVC, antiforgery, cookie authentication, SignalR and health checks ship in `Microsoft.AspNetCore.App`; no extra server SignalR package is required. Serilog's console/configuration support and FluentValidation arrive transitively. EF/Identity packages share the same patch version. Bootstrap 5.3.3 is currently vendored from the MVC template. Chart.js, the browser SignalR client, and a QR library will be pinned when their phases need them. Do not install the legacy FluentValidation MVC auto-validation package; invoke validators in application services with `ValidateAsync`.

## Solution and folder structure

```text
ServeFlow.sln
Directory.Build.props             # net10.0, nullable, analyzers, warnings as errors
Directory.Packages.props          # centrally pinned versions
global.json                       # stable .NET 10 selection
src/
  ServeFlow.Domain/               # Phase 2: Entities, Enums, ValueObjects
  ServeFlow.Application/
    DependencyInjection.cs        # Phase 4+: Features, DTOs, Validators, Abstractions
  ServeFlow.Infrastructure/
    Configuration/SqlServerConnection.cs
    Persistence/ServeFlowDbContext.cs
    DependencyInjection.cs        # Phase 2+: mappings, migrations, Identity
  ServeFlow.Web/
    Controllers/HomeController.cs
    Health/SqlServerHealthCheck.cs
    Views/Home/Index.cshtml
    Views/Shared/_Layout.cshtml
    Views/_ViewImports.cshtml
    Views/_ViewStart.cshtml
    wwwroot/css/site.css
    wwwroot/js/site.js
    wwwroot/lib/                   # template-supplied Bootstrap and licenses
    Program.cs
    appsettings.json
    appsettings.Development.json
    Properties/launchSettings.json
                                  # Later: Areas/Admin, API controllers, Hubs, ViewModels
tests/
  ServeFlow.UnitTests/SqlServerConnectionTests.cs
  ServeFlow.IntegrationTests/StartupTests.cs
docs/
  architecture.md
  phase-1-setup.md
  roadmap.md
  verification.md
scripts/
  create-solution.sh              # command reference for a fresh directory
```

Project files are `src/ServeFlow.{Domain,Application,Infrastructure,Web}/ServeFlow.<project>.csproj` and `tests/ServeFlow.{UnitTests,IntegrationTests}/ServeFlow.<project>.csproj`. Each restored project has a committed `packages.lock.json`. Paths shown above are relative to the repository root `/home/pc/Documents/restaurant-ordering-project`; this is the exact source map for this milestone.

## Commands to create an equivalent starting solution

The project has already been created here. Run these only in a **new empty directory**, not over this repository. See `scripts/create-solution.sh` for the executable project/reference commands.

```bash
dotnet new sln -n ServeFlow --format sln
dotnet new classlib -n ServeFlow.Domain -o src/ServeFlow.Domain -f net10.0 --no-restore
dotnet new classlib -n ServeFlow.Application -o src/ServeFlow.Application -f net10.0 --no-restore
dotnet new classlib -n ServeFlow.Infrastructure -o src/ServeFlow.Infrastructure -f net10.0 --no-restore
dotnet new mvc -n ServeFlow.Web -o src/ServeFlow.Web -f net10.0 --no-restore
dotnet new console -n ServeFlow.UnitTests -o tests/ServeFlow.UnitTests -f net10.0 --no-restore
dotnet new console -n ServeFlow.IntegrationTests -o tests/ServeFlow.IntegrationTests -f net10.0 --no-restore
```

Then apply the project references and copy the actual configuration/source files described above. These template commands create the shell; they do not regenerate ServeFlow's implementation. Package references are already declared without versions in `.csproj` files because central package management owns versions. Use `dotnet restore` to resolve them.

## Database configuration

Provide a SQL Server instance and database when starting Phase 2. Development integrated authentication works on a suitably configured Windows host; Linux SQL authentication requires an external secret. The default no-password connection allows booting the application but is not a promise that the local machine has SQL Server installed.

For local development, store the full connection string with user secrets:

```bash
dotnet user-secrets set 'ConnectionStrings:ServeFlow' 'Server=localhost,1433;Database=ServeFlow;User Id=serveflow_dev;Password=<your-local-password>;Encrypt=true;TrustServerCertificate=true' --project src/ServeFlow.Web
```

Replace the bracketed password locally. Do not commit credentials. User secrets are a development convenience, not encrypted production storage. Prefer a dedicated least-privilege runtime login; migration permissions belong to a deployment identity.

The environment-variable equivalent is `ConnectionStrings__ServeFlow`. Supply it with your host's secret facility. [.env.example](../.env.example) documents variable names only; ASP.NET Core does not automatically load `.env` files.

Production has **no default connection** and startup fails when configuration is missing or malformed, when encryption is disabled, or when certificate validation is bypassed. Set `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__ServeFlow`, and `AllowedHosts` to your actual domain. Terminate TLS appropriately and configure trusted reverse proxies when deploying. Development HTTP is intentional for a simple local start; the `https` launch profile is also available:

```bash
dotnet dev-certs https --trust
dotnet run --project src/ServeFlow.Web --launch-profile https
```

## Configuration decisions

`Program.cs` is the composition root. It registers Application and Infrastructure, MVC, antiforgery, Problem Details, Serilog, and health checks. It never creates/migrates the database or seeds credentials. Unknown routes return normal HTTP error responses. Production HSTS and HTTPS redirection are enabled; CSP and content-type protections apply to responses. Theme scripts/styles are local files so the CSP does not require inline-script allowances.

The Phase 1 context was entity-free. Phase 2 has now added the Identity-compatible model, mappings and initial migration; follow [Phase 2 setup](phase-2-database.md). Phase 3 has now registered Identity services/cookies and policies; see [authentication setup](phase-3-authentication.md).

FluentValidation discovery is ready for Phase 4/5 validators. There is no fabricated CRUD service or empty order implementation. No API business endpoints or SignalR hub is exposed until its access rules and behavior exist.

## Phase 2 migration commands (after the model is implemented)

Historical Phase 1 planning commands follow. Phase 2 has now generated `InitialSchema` and added `dotnet-tools.json`; use [the current migration commands](phase-2-database.md) rather than recreating the initial migration or tool manifest:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12
# After implementing and reviewing the complete entity mappings:
dotnet ef migrations add InitialSchema --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --output-dir Persistence/Migrations
dotnet ef database update --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web
# Deployment: generate a reviewed script instead of modifying schema at application startup.
dotnet ef migrations script --idempotent --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --output artifacts/migrations.sql
```

Use `ASPNETCORE_ENVIRONMENT=Development` with user secrets locally, or inject the connection string securely. Recheck the stable EF patch before installing tooling and keep it aligned with the centrally pinned provider/design packages.

The xUnit 4.x release of the `xunit.v3` package uses Microsoft.Testing.Platform 2. `global.json` opts into its .NET 10 runner, so use `dotnet test --solution` rather than the legacy positional solution command. See the [official runner guidance](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

## Build and test

```bash
dotnet restore ServeFlow.sln --locked-mode
dotnet build ServeFlow.sln --no-restore -c Release
dotnet test --solution ServeFlow.sln --no-build -c Release
```

Intentional package upgrades update `Directory.Packages.props`, then run `dotnet restore --force-evaluate` and review lock-file changes. The current tests cover configuration safeguards and HTTP startup independent of SQL Server. Actual database integration and authorization coverage will accompany later milestones.
