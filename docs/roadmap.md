# Incremental delivery plan

| Phase | Deliverable | Exit checks |
| --- | --- | --- |
| 1 — Architecture and setup | Four-layer solution, SQL configuration, real MVC shell, architecture/ER design | Restore, Release build, configuration and startup tests |
| 2 — Database | Domain entities, Identity-backed EF context, constraints, indexes, concurrency mappings, initial migration | Apply on real SQL Server; verify foreign keys, money precision, indexes and concurrency |
| 3 — Authentication | Identity login/logout, role policies, administrator bootstrap through external secrets, employee account management | Anonymous/forbidden access tests, CSRF tests, role isolation, no development accounts in production |
| 4 — Restaurant management | Restaurant settings, category/menu/table CRUD, secure image uploads | Validation, ownership, authorization, optimistic concurrency tests |
| 5 — Ordering | QR/table sessions, digital menu, option selection, cart, transactional order creation, receipts and idempotency | Server totals, snapshot prices, unavailable item rejection, duplicate request and cross-table tests on SQL Server |
| 6 — Real-time features | Authorized kitchen queue and SignalR order tracking, validated state machine/history | Role/transition tests; scoped hub access; reconnect/resync behavior |
| 7 — Analytics and UI | Dashboard, Chart.js, revenue filters, popular items, accessible sidebar and complete responsive themes | Correct successful-payment aggregation, time-zone/date-boundary checks, browser/mobile review |
| 8 — Testing and deployment | Expanded xUnit suite, Docker, CI, deployment/migration runbook, demo provisioning and portfolio screenshots | Full SQL Server integration suite, CI artifact build, container smoke test, production configuration and backup review |

## Current: Phase 8 testing and deployment preparation

All eight implementation milestones now have code and documentation. The full suite passes against SQL Server 2022 with 247 passed and zero skips. Runtime/migration images build in rootless Podman; the production HTTPS smoke checks verify database migrations, CA-validated SQL TLS, real three-role ordering, CSRF, idempotency, lifecycle/receipt persistence, encrypted keys and cookie validity after restart. See [Phase 8 setup](phase-8-deployment.md) and [verification](verification.md).

## Remaining release gates

1. Run the prepared CI workflow on GitHub and review its artifact.
2. Choose and configure deployment infrastructure, trusted certificates, least-privilege database users and secret storage.
3. Rehearse backup/restore and rollback with real persistent storage.
4. Complete restaurant acceptance checks, including QR scans and live kitchen devices.
5. Address explicitly documented feature limits before advertising payment capture, refunds or submitted-order modifications.

Implement each phase as a reviewable change, building on the existing solution. Suggested commits: architecture/setup; database model/migration; authentication/policies; menu/table management; ordering/idempotency; kitchen/realtime; analytics/UI; deployment/portfolio. No commits or hosted workflows were created.
