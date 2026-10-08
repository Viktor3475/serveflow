# Phase 3 — Authentication and employee accounts

Staff sign-in, sign-out, password changes, role policies, administrator provisioning, and employee management are implemented. Configure SQL Server and apply both migrations before using persisted staff accounts. The migration/provisioning and real database cases have not been executed here because no SQL Server test connection is available.

## What works

| Route | Access | Behavior |
| --- | --- | --- |
| `GET /Account/Login` | Public | Accessible sign-in form with antiforgery token |
| `POST /Account/Login` | Public, antiforgery and throttle | Identity password sign-in; account lockout; safe local redirect |
| `POST /Account/Logout` | Authenticated, antiforgery | Ends the cookie session; GET cannot log out |
| `GET/POST /Account/ChangePassword` | Authenticated, antiforgery on POST | Verifies the current password, updates its hash/security stamp, refreshes the current session |
| `GET /Account/AccessDenied` | Public | Returns a 403 access-denied page |
| `GET /Employees` | Administrator | Lists this restaurant's accounts, capped at 500 |
| `GET/POST /Employees/Create` | Administrator, antiforgery on POST | Creates an employee with one staff role and a securely hashed initial password |
| `GET/POST /Employees/Edit/{id}` | Administrator, antiforgery on POST | Changes display name, staff role, or archive state with an optimistic concurrency check |

There is no public registration endpoint. Guests remain outside Identity. Customer is a provisioned role for future registered-customer features, but it is not a valid employee role. Employee email/usernames are immutable through this management interface. Archiving disables access and keeps historical actor references; unchecking the archive field restores an account. Administrators share initial passwords through their organization's secure channel; ServeFlow does not email or log them. Email recovery, administrator password-reset workflows, mandatory first-login changes, and MFA enrollment/challenge UI are not implemented in this milestone. An externally enabled two-factor account is not allowed to bypass its second factor through this login form.

## Policies and session safety

- `Administration`: Administrator.
- `DiningRoom`: Administrator or Waiter.
- `Kitchen`: Administrator or KitchenStaff.
- `Staff`: any staff role; Customer is excluded.

Every staff policy requires authentication and a server-issued restaurant-membership claim. MVC actions require authentication by default through an authorization filter; the landing page, login/access-denied actions, and health endpoints are explicitly public. Other feature controllers will select the corresponding role policy as they are added.

Claims contain user ID, role, restaurant ID and display name. Controllers obtain the actor ID from the authenticated principal; forms contain no restaurant selector or actor identity. Employee services check active administrator status again and derive restaurant ownership from the stored actor. Cross-restaurant targets are rejected. Editing uses Identity's concurrency stamp; stale changes return HTTP 409.

Employee create/update and administrator provisioning use retry-aware serializable transactions so user/role updates commit together. Self-archival and self-demotion are rejected. Before removing administrator access, the service checks that another active administrator remains. Serializable transactions protect this check during simultaneous updates. Role/archive changes rotate the security stamp, invalidating existing sessions. Duplicate normalized emails are also prevented by the `UniqueAccountEmail` migration; reconcile any preexisting duplicates before applying it.

Identity security stamps are validated on **every authenticated request**, and archived users or archived restaurants are rejected. This intentionally adds database queries for prompt revocation in the initial single-restaurant deployment. A later cache/distributed revocation design can reduce that cost without silently lengthening access after deactivation.

Password policy is at least 12 characters, four distinct characters, uppercase/lowercase letters, a digit, and a non-alphanumeric character. Identity's default password hasher is used; no passwords are stored as plaintext. Five failures lock the account for 15 minutes. Login POSTs are limited to ten per minute per direct client IP, with HTTP 429 for excess requests. Configure trusted proxies explicitly when deploying behind one; this release does not trust arbitrary forwarded headers.

Cookie lifetime is eight hours with sliding renewal. The remember-me checkbox selects a persistent cookie with the same configured lifetime. Cookies are HttpOnly and SameSite=Lax. Production uses a Secure `__Host-ServeFlow.Auth` cookie; development HTTP uses `ServeFlow.Auth`. Antiforgery validation applies to login, logout, employee mutations and password changes; future Fetch calls can send `X-CSRF-TOKEN`.

These settings follow [Microsoft's Identity configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0), [cookie principal validation guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), and [antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## Local setup

1. Configure `ConnectionStrings:ServeFlow` using [the SQL Server setup guide](phase-2-database.md).
2. Restore tools and apply the migrations:

```bash
dotnet tool restore
dotnet build ServeFlow.sln -c Release
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --no-build
```

There are now two migrations: `InitialSchema` and `UniqueAccountEmail`. The current complete idempotent deployment script is [ServeFlowSchema.sql](database/ServeFlowSchema.sql). `InitialSchema.sql` remains the Phase 2 historical script.

3. Configure the administrator through development user secrets. Replace the bracketed values with your restaurant details; generate one stable restaurant GUID and retain it for repeat provisioning:

```bash
dotnet user-secrets set 'Bootstrap:RestaurantId' '<restaurant GUID>' --project src/ServeFlow.Web
dotnet user-secrets set 'Bootstrap:RestaurantName' '<restaurant name>' --project src/ServeFlow.Web
dotnet user-secrets set 'Bootstrap:Email' '<administrator email>' --project src/ServeFlow.Web
dotnet user-secrets set 'Bootstrap:DisplayName' '<administrator name>' --project src/ServeFlow.Web
# Prompt locally so the password itself is not embedded in shell history.
read -rsp 'Administrator password: ' serveflow_admin_password
printf '\n'
dotnet user-secrets set 'Bootstrap:Password' "$serveflow_admin_password" --project src/ServeFlow.Web
unset serveflow_admin_password
```

User secrets are a development convenience; production uses an external secret provider. Passwords are preserved exactly, including whitespace, and must satisfy Identity's policy.

4. Run the one-shot provisioning command:

```bash
dotnet run --project src/ServeFlow.Web --configuration Release --no-build --launch-profile http -- --provision-admin
```

This command requires an already-migrated database, provisions the four roles, creates the restaurant if missing, and creates its administrator. It then exits without starting an HTTP listener. Rerunning it accepts the same active administrator but never overwrites a password or promotes an existing ordinary employee. Mismatched restaurants or archived existing accounts are rejected. Credentials are not printed. Provisioning is never triggered by merely starting the site.

5. Remove the bootstrap password after successful provisioning:

```bash
dotnet user-secrets remove 'Bootstrap:Password' --project src/ServeFlow.Web
dotnet run --project src/ServeFlow.Web --configuration Release --no-build --launch-profile http
```

Open **http://localhost:5080/Account/Login**, sign in, and select **Employees** to add your staff. Use **Change password** for your own account. The landing page still loads without SQL Server; persisted login/account operations require it.

## Production configuration

Provide the following through your deployment configuration/secret manager:

```text
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=<your domain>
ConnectionStrings__ServeFlow=<encrypted SQL connection with certificate validation>
DataProtection__KeyPath=<persistent private key-ring directory>
DataProtection__CertificatePath=<path to a PFX certificate containing its private key>
DataProtection__CertificatePassword=<PFX password if required>
```

Production startup requires a persistent Data Protection key directory and a PFX certificate with a private key. Key files are encrypted with that certificate. Mount the directory and certificate securely; instances serving the same application need the same key ring and compatible certificate access. Protect and back up the certificate/key ring and keep old decryption certificates during planned rotation. Development uses the platform's default key storage; HTTP tests use an ephemeral test provider.

Inject the five `Bootstrap__...` settings only for the explicit provisioning command and remove the password afterward. Keep SQL migration permissions separate from runtime credentials. Production requires TLS and the existing SQL encryption/certificate validation settings. The provisioning host follows the same production configuration requirements.

## Source file map

All paths are relative to `/home/pc/Documents/restaurant-ordering-project`.

```text
src/ServeFlow.Application/Security/StaffSecurity.cs
src/ServeFlow.Application/Accounts/AccountContracts.cs
src/ServeFlow.Application/Accounts/EmployeeValidators.cs
src/ServeFlow.Infrastructure/DependencyInjection.cs
src/ServeFlow.Infrastructure/Identity/AccountActivity.cs
src/ServeFlow.Infrastructure/Identity/StaffClaimsPrincipalFactory.cs
src/ServeFlow.Infrastructure/Identity/ActiveSignInManager.cs
src/ServeFlow.Infrastructure/Identity/ActiveSecurityStampValidator.cs
src/ServeFlow.Infrastructure/Identity/AccountService.cs
src/ServeFlow.Infrastructure/Identity/EmployeeService.cs
src/ServeFlow.Infrastructure/Identity/AdministratorProvisioner.cs
src/ServeFlow.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/20261008185512_UniqueAccountEmail.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/20261008185512_UniqueAccountEmail.Designer.cs
src/ServeFlow.Infrastructure/Persistence/Migrations/ServeFlowDbContextModelSnapshot.cs
src/ServeFlow.Web/Program.cs
src/ServeFlow.Web/Controllers/HomeController.cs
src/ServeFlow.Web/Controllers/AccountController.cs
src/ServeFlow.Web/Controllers/EmployeesController.cs
src/ServeFlow.Web/Models/AccountModels.cs
src/ServeFlow.Web/Models/EmployeeModels.cs
src/ServeFlow.Web/Views/Account/Login.cshtml
src/ServeFlow.Web/Views/Account/ChangePassword.cshtml
src/ServeFlow.Web/Views/Account/AccessDenied.cshtml
src/ServeFlow.Web/Views/Employees/Index.cshtml
src/ServeFlow.Web/Views/Employees/Create.cshtml
src/ServeFlow.Web/Views/Employees/Edit.cshtml
src/ServeFlow.Web/Views/Shared/_Layout.cshtml
src/ServeFlow.Web/Views/Home/Index.cshtml
src/ServeFlow.Web/wwwroot/css/site.css
tests/ServeFlow.UnitTests/EmployeeValidationTests.cs
tests/ServeFlow.UnitTests/PersistenceModelTests.cs
tests/ServeFlow.IntegrationTests/AuthenticationTests.cs
tests/ServeFlow.IntegrationTests/IdentityTestStores.cs
tests/ServeFlow.IntegrationTests/IdentitySqlServerTests.cs
tests/ServeFlow.IntegrationTests/SqlServerDatabaseTests.cs
```

No new NuGet packages were needed. Identity, authentication, authorization, Data Protection, rate limiting and MVC antiforgery use the dependencies already installed.

## Verification and remaining checks

```bash
dotnet test --solution ServeFlow.sln --no-build -c Release
```

The executed HTTP tests use real Identity managers, password hashing, cookie middleware, security-stamp validation, authorization and antiforgery handling. Only user/role persistence and restaurant-activity reads are substituted with test stores; they do not claim to verify SQL Server behavior. Tests cover login, local redirect safety, CSRF, role policies, lockout, throttling, password changes, POST-only logout, archived-user/restaurant rejection and stamp revocation. Application validator cases cover staff role inputs and concurrency versions.

Set `SERVEFLOW_TEST_SQLSERVER` to run all 17 real database cases, including six new Identity cases for idempotent provisioning, employee creation, role/archival/stale updates, tenant/actor isolation, self-protection, and simultaneous administrator changes. Each test uses and removes a generated database as described in Phase 2. Without the variable, these cases explicitly skip. See [verification.md](verification.md) for the actual results.

Next is Phase 4: restaurant settings, menu/categories, tables, and secure product image uploads using the policies introduced here.
