# Verification

## Phase 8 verification

Executed on 2026-10-08–09 (Europe/Sofia) with host SDK 10.0.112, runtime/EF 10.0.12 and rootless Podman 6.1.3 on Linux amd64.

| Check | Actual result |
| --- | --- |
| Final Release build | Passed; zero warnings/errors |
| Full automated suite with real SQL Server | **247 passed, 0 failed, 0 skipped** |
| SQL-specific cases | All 41 executed and passed, including the prior 40 skipped cases and one new demo case |
| New guard/proxy cases | Six demo settings and five proxy cases passed; actual middleware rejects an untrusted peer |
| EF tool restore / pending model check | Pinned 10.0.12 restored; no model changes, no migration needed |
| Runtime and migration images | Both built successfully in rootless Podman using locked dependencies and pinned official base-image digests |
| Migration bundle | Both migrations applied on a new real database; second application reported already up to date |
| Production container smoke | Passed: HTTPS, CA/hostname-validated SQL TLS, UID 1654, read-only root, dropped capabilities, encrypted persistent keys and restart |
| Real HTTPS staff workflow | Passed: administrator/waiter/kitchen login and role boundaries, CSRF rejection, server totals, idempotent replay, kitchen-to-waiter lifecycle and private receipt |
| Cookie continuity | Existing production administrator cookie remained valid after restarting the container |
| Demo CLI | Development provisioning succeeded; Production explicitly rejected it before database work |
| Generated demo environment | Mode 0600 confirmed; credentials generated; existing file overwrite refused |
| JavaScript / Bash / Python syntax | All first-party browser scripts and new shell/Python scripts passed checks |
| CI workflow | YAML parsed and official actionlint v1.7.12 passed; Action releases/commit pins verified through official GitHub APIs |
| Release artifacts | Published web output, exported Linux x64 migration bundle and schema script prepared locally |
| Persistent local demo | Podman startup helper exercised with dedicated database/key/image volumes; loopback-only web endpoint |
| Browser portfolio review | Dashboard, tables and menu Razor snapshots from the SQL-backed demo inspected in Chrome and saved as screenshots |
| Hosted GitHub workflow / remote deployment / backup restore | Not executed |

The final build used `dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false`. The SQL suite used `scripts/test-sqlserver.sh --no-build`, with the established `/tmp/serveflow-dotnet` CLI and `/tmp/serveflow-nuget` cache. Final test duration was 23.091 seconds. Each SQL case used its own generated database. The test container exposed only an ephemeral loopback port and was removed afterward. No tests were disabled or marked skipped to pass.

Live execution exposed a missing IHostEnvironment registration for standalone infrastructure providers. Infrastructure now registers the supplied environment when absent. Another test was intercepted by EF's tracked-dependent guard before SQL could check deletion; clearing tracking before that attempted delete makes it exercise the real foreign-key rejection. The initial run had 24 failures, the corrected run had one, and the final suite passed all 247. A test invocation that accidentally passed build arguments to the Microsoft.Testing.Platform runner ran zero cases; it was discarded and the final run used the rebuilt assemblies with `--no-build`.

SQL Server 2025 was pulled and attempted but crashed during startup with SQLPAL errno 11; it never ran tests. SQL Server 2022 succeeded and is digest-pinned in the verification helpers. The first production smoke attempts using Strict/TDS 8 were rejected by the SQL Server 2022 image despite its certificate loading. The final smoke uses mandatory encryption with an explicit disposable CA and hostname validation, keeping `TrustServerCertificate=false`. HTTPS is also validated against the generated certificate, rather than using curl's insecure option. The smoke database uses temporary Developer/sa credentials solely within the isolated verification network; production instructions require a limited runtime login.

The production runtime performs actual HTTP requests against real SQL persistence; it does not substitute the use-case services. It proves one persisted staff lifecycle, not every multi-device/WebSocket path. Existing hub tests still use TestServer Long Polling and substitute membership; native browser WebSocket/phone behavior and scale-out remain acceptance checks. Smoke credentials, certificates and cookies are private temporary files removed on exit. Image build caches remain available for reuse.

The prepared GitHub workflow runs equivalent tests/images/smoke checks and exports artifacts, but no hosted CI execution or public deployment is claimed. Existing Phase 7 screenshots remain labelled fixtures; Phase 8 portfolio pages are captured from a disposable SQL-backed development demo. See [the deployment guide](phase-8-deployment.md) for commands, exact file locations and release limits.

## Phase 7 verification

Executed on 2026-10-08 with SDK 10.0.112 and runtime/EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| Release solution build | Passed; zero warnings/errors |
| Automated suite | 195 passed, 0 failed, 40 explicitly skipped; 235 total |
| New analytics unit cases | 15 passed, including real SQL Server query translation without database execution |
| New dashboard HTTP cases | 8 passed using real MVC/cookies/policies and a substituted analytics backend |
| Chart.js vendoring | Stable npm 4.5.1 verified; local bundle/map, MIT license and integrity/hash provenance included |
| JavaScript syntax | `site.js` and `dashboard.js` passed `node --check` |
| EF pending-model check | Passed; no migration required |
| Browser visual review | Chrome desktop light/dark and 390px-wide mobile iframe using labelled Razor UI fixture |
| Mobile interactions | Sidebar opening and Escape/focus restoration checked; final page width equals scroll width |
| Real SQL Server suite | Not executed; prior 36 cases plus four analytics cases skipped |
| Native phone / persisted analytics / deployment | Not executed |

Build used `dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false`; tests used `dotnet test --solution ServeFlow.sln --no-build -c Release` through approved local IPC execution. The test run also set `SERVEFLOW_UI_CAPTURE_DIR=/tmp/serveflow-ui-preview` to export test-only Razor output. Both used the existing `/tmp/serveflow-dotnet` CLI and `/tmp/serveflow-nuget` cache paths. EF used Infrastructure as project, Web as startup, Release and `--no-build`.

The SQL translation check exercises shared aggregate expressions with SQL Server's EF provider but never connects. It does not prove database execution. The four SQL cases explicitly skip because `SERVEFLOW_TEST_SQLSERVER` is unset. HTTP fixtures test policy/serialization/rendering and are not evidence of real financial results. No cases were disabled to pass. Browser review found cramped mobile date inputs and a panel overflow; CSS corrections gave the inputs full readable widths and contained the page. Subsequent review confirmed these fixes. The mobile test was a 390px iframe, not an emulated/native phone. See [Phase 7 guide](phase-7-analytics-ui.md) for definitions and live checks.

## Phase 6 verification

Executed on 2026-10-08 with .NET SDK 10.0.112 and runtime/EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| Dependency restore | Passed; .NET SignalR client 10.0.12 locked for integration tests |
| Official browser-client vendoring | npm stable version verified as 10.0.11; JS/map, MIT license and integrity/hash provenance included |
| Final Release solution build | Passed; zero warnings and errors |
| Final automated suite | 172 passed, 0 failed, 36 explicitly skipped; 208 total |
| New role/state unit cases | 19 passed |
| New HTTP and actual hub-transport cases | 13 passed, including four tests using .NET clients over TestServer Long Polling |
| JavaScript syntax checks | `order-live.js`, `operations.js` and updated `dining-receipt.js` passed |
| EF pending-model-changes command | Passed; no migration needed |
| Real SQL Server cases | Not executed: 11 database, 6 Identity, 7 management, 7 ordering, 5 workflow cases explicitly skipped |
| Browser WebSocket/mobile visual interaction review | Not executed |
| Persisted multi-device workflow / production deployment | Not executed |

The final Release command was `dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false`. Tests ran with `dotnet test --solution ServeFlow.sln --no-build -c Release` through the approved IPC execution path. Both used `DOTNET_CLI_HOME=/tmp/serveflow-dotnet` and `NUGET_PACKAGES=/tmp/serveflow-nuget`. JavaScript checks used `node --check`. The EF check used Infrastructure as project, Web as startup, Release and `--no-build`.

The transport tests start actual SignalR connections rather than invoking a hub class directly. They verify anonymous staff denial, bad receipt denial, delivery only to the subscribed order, stop/start resubscription, subscription renewal, restaurant isolation, security-stamp revocation and the 20-invocation bound. The notifier test also forces an access-check outage and verifies that publication does not propagate a failure. Membership/persistence in these transport tests is substituted; they do not prove the live database authorization queries. HTTP tests exercise real staff cookies/policies, kitchen restrictions, server actor selection, noncached queue rendering, CSRF, stale-save 409 and same-origin hub enforcement. All final tests passed; no cases were disabled.

The five new SQL cases are discovered but skipped. They are designed to verify lifecycle/history, atomic table release with another active order, required cancellation reasons, concurrent acceptance, kitchen/waiter action separation and real cross-restaurant/archive rejection. No SQL Server connection is configured, and no database execution or committed-event test is claimed. The existing model remains unchanged. Browser syntax checks and Long Polling tests do not prove WebSocket/mobile behavior. See [Phase 6 setup](phase-6-realtime.md) for live workflow and deployment limits.


## Phase 5 verification

Executed on 2026-10-08 with .NET SDK 10.0.112 and runtime/EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| NuGet restore with QRCoder 1.8.0 | Passed; dependency locks updated |
| Release solution build | Passed; zero warnings and errors |
| Automated suite | 140 passed, 0 failed, 31 explicitly skipped; 171 total |
| New pricing/order/option validation cases | 22 passed |
| New ordering HTTP cases | 14 passed using real MVC, cookies, Data Protection, CSRF, rate limits and SVG rendering with a substituted ordering backend |
| JavaScript syntax | All three new scripts passed `node --check` |
| EF pending-model-changes command | Passed; no new migration required |
| Real SQL Server cases | Not executed: 11 database, 6 Identity, 7 management and 7 ordering cases explicitly skipped |
| Live persisted ordering, concurrent commits and QR scans | Not executed; no test database configured |
| Browser/phone visual and interaction review | Not executed |
| Production deployment | Not executed |

Final commands were `dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false` and `dotnet test --solution ServeFlow.sln --no-build -c Release`. The test command used approved local IPC execution. Both used `DOTNET_CLI_HOME=/tmp/serveflow-dotnet` and `NUGET_PACKAGES=/tmp/serveflow-nuget`. The EF check used Infrastructure as the project, Web as startup, and `--configuration Release --no-build`. `node --check` ran for `dining-start.js`, `dining-menu.js` and `dining-receipt.js`.

Pricing tests cover decimal totals with per-unit options, stale product/option versions, unavailable/missing products and cross-product selections. Validation tests cover quantity bounds, duplicate options, unit limits, malformed/null collections, receipt token format, bounded instructions and option prices. HTTP tests cover anonymous noncached pages, protected/tampered table cookies, CSRF on exchange and order POSTs, receipt capabilities/recovery, 201 versus replay 200, waiter identity, staff restrictions, QR download and exchange throttling. The first run's two failures were relative-URL expectations for valid absolute access-denied redirects; the corrected tests assert the actual destination path. No tests were disabled to pass.

The seven new SQL cases are discovered but skipped. They are designed to verify snapshot/history/occupancy transactions, simultaneous duplicate submissions, changed payload conflicts, real cross-table/restaurant rejection, QR revocation, unavailable selections, reserved-table/staff boundaries and customer limits. Their existence is not proof that SQL Server accepted the operations. HTTP tests substitute ordering persistence and do not validate SQL transactions. Syntax checks do not establish browser interaction correctness. Follow [Phase 5 setup](phase-5-ordering.md) to run the live workflows and database cases.


## Phase 4 verification

Executed on 2026-10-08 with .NET SDK 10.0.112 and runtime/EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| Release solution build | Passed; zero warnings and errors |
| Automated suite | 104 passed, 0 failed, 24 explicitly skipped; 128 total |
| New management validation and native image-store cases | 26 passed |
| New management HTTP cases | 24 passed, using real cookies/policies/CSRF/Razor and a substituted management backend |
| EF pending-model-changes command | Passed; no new migration required |
| Real SQL Server cases | Not executed: 11 database, 6 Identity and 7 management cases explicitly skipped |
| Live persisted CRUD/image references | Not executed; SQL Server test connection not configured |
| Browser visual review and production deployment | Not executed |

The final Release build used `dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false`. The final test command was `dotnet test --solution ServeFlow.sln --no-build -c Release`, with approved local IPC execution. Both used `DOTNET_CLI_HOME=/tmp/serveflow-dotnet` and `NUGET_PACKAGES=/tmp/serveflow-nuget`. The model check used the Infrastructure project and Web startup project with `--configuration Release --no-build`. Dependency restore also completed after adding the pinned SkiaSharp packages.

The initial HTTP run detected that MVC's form value provider could bind both a posted model ID and an action parameter from the same posted field. Explicit `[FromRoute]` binding corrected the comparison. Four passing regression cases cover category edit, product edit, table edit and table status. All final tests passed without disabling cases.

Executed image tests used the real native decoder and temporary filesystem storage. They verify decoding/re-encoding, removal of an appended payload, filename/path rejection, format/signature checks and bounded input/dimensions. Web tests cover role restrictions, anonymous challenges, forged actor fields, CSRF, stale-save HTTP 409 and management form rendering. SQL service tests exist for ownership, archive rules, stale versions, waiter permissions, active-order table protection, duplicate numbers and image reference updates, but their skips do not prove SQL behavior. See [Phase 4 setup](phase-4-management.md).


## Phase 3 verification

Executed on 2026-10-08 with .NET SDK 10.0.112 and runtime/EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| Release solution build | Passed; zero warnings and errors |
| Automated suite | 54 passed, 0 failed, 17 explicitly skipped |
| HTTP Identity/cookie/policy cases | 21 passed, using real managers/hashing/cookies and substituted persistence |
| Employee validator cases | 8 passed |
| Unique-account-email migration | Generated; designer and snapshot included |
| Current idempotent SQL script | Generated at `docs/database/ServeFlowSchema.sql` |
| EF pending-model-changes command | Passed; no changes since the latest migration |
| Real SQL Server cases | Not executed: 11 model/database cases and 6 Identity transaction cases skipped |
| Provisioning command missing-secret check | Passed; stopped before SQL operations or an HTTP listener |
| Actual administrator provisioning | Not executed; SQL Server not configured |
| Browser visual review and production deployment | Not executed |

The executed command was `dotnet test --solution ServeFlow.sln --no-build -c Release`, using the same managed-environment CLI/NuGet paths and approved IPC execution as earlier phases. The Release build used `-m:1 -p:UseSharedCompilation=false` to avoid sandbox compiler pipe restrictions.

The first HTTP suite exposed a process-global Serilog initialization race between parallel test hosts. Host-specific logger instances resolved it; the final suite passed with parallel hosts enabled. No authentication or authorization tests were silently disabled to obtain a passing result.

Coverage includes valid staff login, role/restaurant policy boundaries, anonymous challenges, antiforgery rejection, safe return URLs, lockout, login throttling, password hash changes, POST-only logout, archive rejection and security-stamp revocation. The SQL Server cases remain explicit skips, not evidence that provisioning or transaction/concurrency behavior has executed against a database.

## Phase 2 verification

Executed on 2026-10-08 with SDK 10.0.112, runtime 10.0.12 and EF tooling 10.0.12.

| Check | Actual result |
| --- | --- |
| Release solution build | Passed; zero warnings and errors |
| Initial migration generation | Passed; model, designer and snapshot generated |
| Idempotent SQL Server script generation | Passed; `docs/database/InitialSchema.sql` generated |
| EF pending-model-changes command | Passed; no model changes since the migration |
| Automated tests | 25 passed, 0 failed, 11 explicitly skipped |
| Live SQL Server migration/constraint/concurrency cases | Not executed; no test connection or running SQL Server container available |

Final commands executed:

```bash
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet test --solution ServeFlow.sln --no-build -c Release
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet ef migrations has-pending-model-changes --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build
```

Fourteen new model checks passed in addition to the eleven Phase 1 cases. Eleven new SQL Server cases were discovered and skipped with an explicit `SERVEFLOW_TEST_SQLSERVER` configuration message. They were not reported as successful database execution. InMemory and SQLite were not used as substitutes for SQL Server.

The generated SQL was inspected for table definitions, composite foreign keys, filtered unique indexes, money precision, rowversion columns, check constraints and non-cascading domain deletes. SQL generation and model validation do not prove that the server has accepted/applied the migration. The remaining database exit checks are documented in [Phase 2 setup](phase-2-database.md).

## Phase 1 verification

Executed on 2026-10-08 with .NET SDK 10.0.112 and runtime 10.0.12.

| Check | Actual result |
| --- | --- |
| NuGet restore for all six projects | Passed; package lock files generated |
| Release solution build | Passed; zero warnings, zero errors |
| xUnit configuration tests | 8 passed |
| MVC startup integration tests | 3 passed |
| Total automated tests | 11 passed, 0 failed, 0 skipped |

Commands used in this managed environment:

```bash
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet restore ServeFlow.sln
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet build ServeFlow.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false
DOTNET_CLI_HOME=/tmp/serveflow-dotnet NUGET_PACKAGES=/tmp/serveflow-nuget dotnet test --solution ServeFlow.sln --no-build -c Release
```

`-m:1` and disabled shared compilation avoid the managed sandbox's compiler pipe restrictions; they are environment workarounds rather than project requirements. Restore required public NuGet network access. Test execution needed the approved execution path because Microsoft.Testing.Platform uses local IPC pipes restricted in the sandbox.

The initial test command exposed the legacy runner mismatch, which was corrected by selecting Microsoft.Testing.Platform in `global.json`. Tests use ephemeral Data Protection keys and an intentionally unreachable database connection. The page and process health checks work without contacting SQL Server.

Validated behavior: missing/malformed SQL configuration rejection, production encryption and certificate validation requirements, sanitized configuration exceptions, HTTP home-page rendering, CSP headers, database-independent liveness, and unknown route handling.

Not executed: live SQL Server connectivity, migrations, database constraints/concurrency, browser visual inspection, production deployment, authentication/authorization, menu/order workflows, payment handling, or SignalR. These require their corresponding later phases. This milestone is a verified foundation, not a production-ready completed application.
