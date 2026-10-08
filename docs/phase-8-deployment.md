# Phase 8 — Tests, Podman and deployment preparation

ServeFlow now includes a Docker-compatible multi-stage build, a separate EF migration image, disposable Podman SQL tests, production HTTPS smoke checks, explicit development demo provisioning and a GitHub Actions workflow. These files prepare a release; no remote environment was deployed and no GitHub workflow was run from this workspace.

## Build and verify with Podman

Use a Linux x86-64 host with rootless Podman, .NET 10 SDK, Python 3, OpenSSL, curl and Node.js for syntax checks. SQL Server containers require enough memory (at least 2 GB for SQL alone; allow more for builds/tests). Microsoft documents supported SQL Server container architectures and deployment in its [container guide](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17).

```bash
dotnet restore ServeFlow.sln --locked-mode
dotnet tool restore
dotnet build ServeFlow.sln -c Release --no-restore
scripts/test-sqlserver.sh --no-build
podman build --target runtime -t localhost/serveflow:phase8 .
podman build --target migrations -t localhost/serveflow-migrations:phase8 .
scripts/smoke-containers.sh
```

Set `CONTAINER_ENGINE=docker` to use the same verification scripts with Docker. The SQL script generates a private password, binds an ephemeral port to loopback, waits for a real query, exports the test connection only to its process and removes its own container on exit. Each database test creates/deletes a generated database and does not alter an existing user database. Developer edition is for development/test use. The helper sets `ACCEPT_EULA=Y` for this explicitly requested local verification. Credentials are never printed or committed. Tests require database create/drop permissions on the supplied test instance; never point `SERVEFLOW_TEST_SQLSERVER` at production.

The verified default SQL image is SQL Server 2022, pinned by digest. SQL Server 2025 was also attempted locally but crashed before readiness with SQLPAL errno 11; no 2025 test success is claimed. You can override `SQLSERVER_IMAGE` for a separately validated version. Runtime and SDK images are also digest-pinned. Review security updates and refresh pins deliberately; Dependabot handles Docker, NuGet and Action updates, while SQL image references in shell scripts need manual review.

The smoke script creates its own network, certificates, database and volumes. It applies both migrations with the migration image, re-applies the bundle, seeds three staff accounts through the Development-only command, rejects that command in Production, then runs the actual web image in Production. The runtime uses HTTPS, a read-only root filesystem, dropped capabilities, a small writable `/tmp` and named volumes. SQL uses TLS 1.2 with CA/hostname validation (`Encrypt=true;TrustServerCertificate=false`); the disposable CA is supplied through `SSL_CERT_FILE`, not a certificate-validation bypass. The initial Strict/TDS 8 attempt failed on the SQL Server 2022 image; this check uses its supported mandatory-encryption path. It checks health, CSRF, staff role access, persisted order lifecycle, private receipts, encrypted Data Protection files and a staff cookie after restart. It removes only resources with its unique task names.

## Local development demo

The demo has administrator, waiter and kitchen accounts; customers use QR access without accounts. Passwords are generated externally into a mode-0600 environment file. No shared/default password is embedded in application code. The demo seeds one category, three products and six tables when the configured restaurant has no categories/tables. It does not fabricate payments or analytics. Re-runs preserve existing credentials/roles/content. Existing accounts with incompatible ownership or roles cause rejection. Use a dedicated demo database and restaurant: the administrator provisioning step commits separately from the menu/employee seed, so retry a failed seed rather than assuming the whole command rolled back.

After building the images above, `scripts/start-demo.sh` performs the local setup and readiness checks below. It refuses to replace existing demo containers, reuses a private environment file, and retains data volumes. Alternatively, run the individual commands:

```bash
python3 scripts/create-demo-env.py .env.demo
podman network create serveflow-demo
podman volume create serveflow-demo-db
podman volume create serveflow-demo-keys
podman volume create serveflow-demo-images
podman run -d --name serveflow-demo-sql --network serveflow-demo --network-alias sql \
  --env-file .env.demo -v serveflow-demo-db:/var/opt/mssql:U,Z \
  mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090
```

Wait until this command succeeds (the password stays inside the container):

```bash
podman exec serveflow-demo-sql sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1"'
podman run --rm --network serveflow-demo --env-file .env.demo localhost/serveflow-migrations:phase8
podman run --rm --network serveflow-demo --env-file .env.demo localhost/serveflow:phase8 --provision-demo
podman run -d --name serveflow-demo-web --network serveflow-demo --env-file .env.demo \
  -p 127.0.0.1:5080:8080 -v serveflow-demo-keys:/data/keys -v serveflow-demo-images:/data/images \
  localhost/serveflow:phase8
```

Open **http://localhost:5080/Account/Login**. Read your private `.env.demo` locally for the three email/password pairs. Do not upload it or add it to screenshots. The generated origin is loopback; QR scans from a phone need a reachable explicitly configured development origin. The local demo uses Development, SQL `sa` and a trusted-server-certificate exception; it is intentionally loopback-only and must not be exposed publicly. Development key files can be persisted but are not certificate-encrypted.

Stop with `podman stop serveflow-demo-web serveflow-demo-sql`. Start the same containers again with `podman start serveflow-demo-sql serveflow-demo-web`; wait for SQL readiness before using the app. To rebuild, stop/remove only the demo web container, then create it with the same environment and volumes. Removing a container does not remove these named volumes. Keep the private environment file: regenerating it changes passwords/restaurant IDs, and the provisioner does not reset old accounts. Do not run a global prune/reset to manage this demo.

## Production configuration and migration procedure

Use [.env.example](../.env.example) as a key reference, not as ready credentials. Supply secrets from your deployment platform or protected mounted files; never bake them into images. Configure:

- Explicit `AllowedHosts` and an external HTTPS `Ordering:PublicOrigin`.
- A reachable SQL database with encrypted transport and a verified server certificate. Install the issuing private CA in the container trust store when required. Use a runtime login with only the application's data permissions; no `sa`, `db_owner` or migration privileges.
- An absolute image-store path on persistent storage with write access for UID 1654.
- Persistent Data Protection keys plus a private-key PFX and external PFX password in Production. Keep that certificate and prior decryption certificates available for existing keys. Losing keys/certificates invalidates staff/table sessions; losing image storage breaks product images.
- TLS directly through Kestrel, or a trusted reverse proxy that forwards WebSocket upgrades, preserves the public Host and sets X-Forwarded-For/Proto. Configure only that peer's exact IP in `ReverseProxy:KnownProxies`; no wildcard, hostname or blanket proxy trust. The middleware runs before HTTPS redirection, authentication, logging and IP rate limiting. HTTPS redirection behind a proxy uses `ASPNETCORE_HTTPS_PORT=443`. Restrict direct container access so clients cannot impersonate the trusted proxy.

The proxy configuration follows [Microsoft's guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0). SQL certificate validation requirements are described in [the SqlClient guide](https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation?view=sql-server-ver17).

Release procedure:

1. Run all tests and inspect the generated idempotent SQL script and migration bundle for the target schema. Back up the database, image store, Data Protection keys and encryption certificates; verify a restore on a separate environment.
2. Put writes into a maintenance window. Use a separate migration login with the required schema permissions. Run the migration image once with its private environment file and network access to the target database. The bundle reads `ConnectionStrings__ServeFlow`; no connection-string command argument is needed. Do not run migrations automatically when starting web instances.
3. Provision the first administrator with `--provision-admin`, supplying the existing `Bootstrap__*` keys through a one-shot protected environment file and the same production certificates/configuration. Delete that provisioning environment file after use according to your secret-management policy. `--provision-demo` always rejects Production before database work.
4. Start the immutable runtime image with the normal limited database login, persistent volumes and TLS/proxy configuration. Verify `/health/live`, `/health/database`, staff login, QR generation, ordering, kitchen updates and analytics before enabling traffic. Health endpoints confirm process/database connectivity, not migration completeness or business reconciliation; restrict detailed operational access through your proxy.
5. Record image digests and migration state. For rollback, use the prior compatible application image only if the new schema remains compatible; do not blindly run down migrations. Restore a verified backup when necessary and account for orders accepted since it was taken. Test this procedure before a restaurant relies on the service.

Example commands (use private paths outside the checkout, with real configuration):

```bash
podman run --rm --network YOUR_PRIVATE_NETWORK --env-file /secure/migration.env \
  localhost/serveflow-migrations:phase8
podman run --rm --network YOUR_PRIVATE_NETWORK --env-file /secure/bootstrap.env \
  -v /secure/certificates:/run/secrets:ro,Z -v serveflow-keys:/data/keys \
  localhost/serveflow:phase8 --provision-admin
```

The runtime's SignalR connection registry and rate limiters are in-process. Deploy one web instance for the current implementation. Multiple replicas require a tested scale-out design/backplane, distributed rate limiting and shared key/image storage; a load balancer alone is insufficient. Payment capture, refunds, modified submitted orders and public customer account registration are not implemented and should not be advertised as delivered features.

## CI and portfolio

`.github/workflows/ci.yml` runs on pushes to main, pull requests and manual dispatch. It restores locked dependencies, builds, checks all local scripts/model completeness, runs the full SQL Server suite, builds both images, executes the production smoke workflow and publishes the web output, Linux x64 migration bundle and schema script as a 14-day artifact. It has read-only repository permissions, pinned official Action commits, no publish/deploy step and no production secrets. This workflow was prepared and YAML-parsed locally; a hosted GitHub run remains necessary after putting the project in a repository. The exported migration binary may need `chmod +x` after extracting an artifact. It requires the matching .NET/ASP.NET runtime and target platform.

Existing [desktop](screenshots/phase-7-desktop-fixture.jpg) and [mobile](screenshots/phase-7-mobile-fixture.jpg) screenshots show clearly labelled Razor UI fixture data. They are visual evidence, not production financial results. Phase 8 adds [the SQL-backed dashboard](screenshots/phase-8-dashboard-demo.jpg), [table management](screenshots/phase-8-tables-demo.jpg) and [menu management](screenshots/phase-8-menu-demo.jpg), captured from actual Razor responses after the disposable HTTPS workflow. Their data is a development demo, and the capture review serves static snapshots; the separate HTTPS smoke check verifies persistence and interactions. Set `SERVEFLOW_SMOKE_REVIEW_DIR` when running the smoke script to export these pages locally. Avoid QR/receipt capabilities, credentials, cookies and personal data in public screenshots.

## Changed-file map

All paths are relative to the project root.

| File | Purpose |
| --- | --- |
| `Dockerfile`, `.dockerignore` | Digest-pinned runtime/build/migration targets; excludes credentials and local artifacts |
| `.config/dotnet-tools.json` | EF CLI 10.0.12 tool pin |
| `.env.example` | Production configuration-key reference |
| `.github/workflows/ci.yml`, `.github/dependabot.yml` | Verification/artifact workflow and update proposals |
| `scripts/test-sqlserver.sh` | Disposable real SQL suite for Podman/Docker |
| `scripts/smoke-containers.sh` | Production-mode migration/TLS/key/restart and HTTP workflow check |
| `scripts/start-demo.sh` | Start the persistent loopback-only Podman demo without replacing existing containers |
| `scripts/create-demo-env.py` | Private generated local demo credentials |
| `scripts/verify-container-http.py` | Real cookie/CSRF/role/order/lifecycle/receipt verification over HTTPS |
| `src/ServeFlow.Web/Configuration/TrustedProxyConfiguration.cs` | Exact peer-IP proxy trust and bounded forwarded headers |
| `src/ServeFlow.Web/Program.cs` | Proxy middleware, explicit demo command and configured key persistence |
| `src/ServeFlow.Infrastructure/Identity/DevelopmentDemoSettings.cs`, `DevelopmentDemoProvisioner.cs` | Development-only external credentials and idempotent staff/menu/table seed |
| `src/ServeFlow.Infrastructure/DependencyInjection.cs` | Demo registration and host-environment dependency |
| `tests/ServeFlow.UnitTests/DevelopmentDemoSettingsTests.cs` | Six production guard/configuration cases |
| `tests/ServeFlow.IntegrationTests/TrustedProxyTests.cs` | Five proxy configuration/actual middleware cases |
| `tests/ServeFlow.IntegrationTests/IdentitySqlServerTests.cs` | Real demo idempotency/password/role/menu test |
| `tests/ServeFlow.IntegrationTests/SqlServerDatabaseTests.cs` | Ensures deletion is checked by SQL foreign key rather than tracked-entity guard |
| `docs/screenshots/phase-8-dashboard-demo.jpg`, `phase-8-tables-demo.jpg`, `phase-8-menu-demo.jpg` | Reviewed SQL-backed demo portfolio captures |
| `README.md`, `docs/roadmap.md`, `docs/verification.md`, this guide | Run instructions, delivery status and actual results |

See [verification](verification.md) for the final executed results and the limits of each check. The remaining release gates are a hosted CI run, chosen deployment infrastructure, real certificates/secrets, backup/restore rehearsal and restaurant acceptance checks. No public deployment or backup restore was performed here.
