# Production-readiness plan

## Phase 1 assessment — 2026-10-30

EthioServe is a modular monolith with a clean ASP.NET Core 10 backend (Domain, Application, Infrastructure, API and Tests) and an Angular 22 standalone-component frontend. PostgreSQL is the system of record. The architecture is appropriate for the current marketplace scope; splitting it into microservices would add deployment and operational cost without a demonstrated need.

### Verified baseline

- Backend Release build succeeds with zero warnings and errors.
- Frontend production build succeeds.
- Frontend unit tests: 374 passing.
- Backend unit, architecture and schema tests: 416 passing when Docker is available.
- Backend integration tests use Testcontainers and require Docker Desktop or a compatible Docker daemon.

### Delivered capabilities

- JWT authentication, rotating hashed refresh tokens, lockout, password reset, rate limiting, role enforcement, RFC 7807 errors and structured logging.
- Public provider discovery with service and location filtering; provider self-service; provider review and verification by administrators.
- Customer request lifecycle, safe image attachments, provider quotations, appointment scheduling, notifications and authenticated SignalR notification delivery.
- Completed-job payments with customer checkout, idempotent attempts, provider earnings, audit/notification coverage, and a development-only simulated gateway. No real money is processed.
- EF Core migrations, database constraints, optimistic concurrency and audit records for sensitive actions.
- Dockerfiles, Compose health checks, GitHub Actions and broad unit/integration coverage.

### Prioritized gaps

1. **Deployment correctness (complete):** Compose supplies the required JWT signing key, initializes an empty database, persists request uploads, avoids default production seed credentials and forwards `/api` and `/hubs` to the private API container. The isolated Compose startup and frontend proxy paths were validated.
2. **Operations:** the deployment guide now documents production configuration, migration and rollout steps, backups, AWS ECS/Fargate/RDS/EFS topology, secrets management, logs and TLS termination. Production infrastructure and restore procedures still need live verification.
3. **Marketplace completeness:** completed-job reviews, reversible admin review moderation, provider-scoped customer-provider messaging, provider availability calendars and the mock payment workflow now have customer/provider UI, ownership checks, notifications and audit coverage. Real payment providers and payouts and portfolios are not complete product flows.
4. **Authentication hardening (in progress):** password recovery advertises delivery availability, fails closed when no provider is configured, and has an Amazon SES adapter using the AWS credential chain; a hosted worker cleans up expired authentication tokens. SES identity/task-role verification, durable background email delivery, verified-email and optional MFA design remain.
5. **Observability (in progress):** distinct liveness and database-readiness probes are available. Production alerting dashboards and live infrastructure probes remain.

## Implementation checklist

- [x] Preserve clean working tree and establish build/test baseline.
- [x] Confirm no committed `.env` file and avoid exposing local secrets.
- [x] Repair Compose secret contract, first-run database initialization, and frontend API/SignalR proxying.
- [x] Keep generated frontend artifacts out of the container build context.
- [x] Validate a complete Compose startup, frontend-to-API proxy, and SignalR negotiation path.
- [x] Add production/AWS deployment runbook.
- [x] Fail closed and visibly disable password recovery when email delivery is not configured.
- [x] Add scheduled cleanup for expired and used authentication tokens.
- [x] Separate liveness and database readiness health endpoints.
- [ ] Configure AWS authentication, verify the production estimate stays within the $84/month budget, and deploy to `eu-west-2`.
- [x] Implement completed-job reviews and the reversible admin moderation workflow after confirming its policy.
- [x] Implement the completed-job review workflow through API, UI, notifications and automated tests.
- [x] Implement customer payment checkout/history and provider payment/earnings using a mock gateway that is disabled in Production.
- [x] Add payment workflow integration and frontend API coverage.
- [x] Exercise job completion through the appointment API before payment, and add customer checkout/provider earnings UI tests.
- [x] Add private provider-scoped customer/provider conversations with participant authorization and frontend coverage.
- [x] Add recurring provider hours and date exceptions with appointment enforcement and Addis Ababa time handling.
- [x] Add admin review hide/restore controls; hidden reviews are removed from public ratings and results.
- [x] Add the Amazon SES v2 email adapter with region and verified-sender configuration.
- [ ] Verify the SES sender identity, production account status, task role, and live password-reset delivery.
- [ ] Add workflow-level integration and frontend coverage for each new capability.
