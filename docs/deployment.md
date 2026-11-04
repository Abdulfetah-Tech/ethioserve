# Deployment guide

## Local container stack

1. Copy `.env.example` to `.env`.
2. Set a unique `POSTGRES_PASSWORD` and a unique `JWT_SIGNING_KEY` of at least 32 characters. Do not use either example value in a shared or production environment.
3. Start the stack:

   ```powershell
   docker compose up --build
   ```

4. Open `http://localhost:4200`. The frontend proxies `/api` and `/hubs` to the private API container. Readiness endpoints are available at `http://localhost:5102/health` and `http://localhost:5102/health/ready`; `/health/live` does not depend on PostgreSQL.

The Compose stack sets `Database__ApplyMigrationsOnStartup=true`. This is suitable for its one API container: it applies EF migrations and idempotently adds required roles, Ethiopian locations and catalog records. It does not create a production administrator or demo users.

Request uploads are stored in the Compose `ethioserve-uploads` volume. The API's `Storage__RootPath` must point to a durable mount; without it, the application falls back to a temporary directory.

The mock payment gateway is enabled by default only in Development and Testing. It simulates success, decline, and cancellation; it does not move money or collect card or mobile-wallet details. Production always disables the mock gateway, even if `Payments__AllowMock=true` is supplied. Chapa, Telebirr, and provider payouts are not integrated yet.

Password recovery uses Amazon SES when `Email__Provider=Ses`; otherwise delivery is unavailable outside Development email-body logging and test substitutes. For SES, verify the sender identity in the configured region, move the account out of the SES sandbox before sending to arbitrary recipients, and grant the API workload's IAM role `ses:SendEmail` for that identity. The SDK uses the AWS default credential chain; use the ECS task role in production and never place AWS access keys in application configuration.

## AWS target architecture

- **Frontend:** static Angular assets in S3 + CloudFront, or a containerized frontend service when same-origin reverse proxying is required.
- **API:** ECS Fargate service behind an Application Load Balancer. Run at least two tasks across availability zones after the database is initialized.
- **Database:** Amazon RDS for PostgreSQL with Multi-AZ enabled where the service-level objective requires it. Keep the database private; allow ingress only from the API security group.
- **Request uploads:** the current implementation uses local filesystem storage. Mount a private, encrypted EFS file system at the same `Storage__RootPath` on every API task, and back it up. Do not use Fargate task storage or a per-task volume for customer uploads; files would be lost or unavailable when tasks are replaced or scaled. An object-storage implementation is a future alternative.
- **Images:** build in CI and push immutable image tags to Amazon ECR.
- **Secrets:** place the connection string and `Jwt__SigningKey` in AWS Secrets Manager; inject them into ECS task definitions. Never put them in images, task-definition source, GitHub variables or `.env` files.
- **Email:** use Amazon SES in `eu-west-2` with a verified sender identity and an ECS task role limited to `ses:SendEmail` for that identity.
- **TLS and DNS:** terminate TLS at the ALB using ACM. Forward `X-Forwarded-Proto` and configure the API task to trust only the ALB or service network proxy.
- **Logs and alerts:** ship container stdout to CloudWatch Logs; alarm on ALB 5xx, unhealthy targets, ECS task restarts, RDS storage/connection pressure and authentication-rate-limit spikes.

## Production preflight and rollout

Before deploying, configure the API task with `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Default` and `Jwt__SigningKey` from Secrets Manager, `Database__ApplyMigrationsOnStartup=false`, the public frontend origin in `Cors__AllowedOrigins__0`, the public frontend URL in `App__FrontendBaseUrl`, `Storage__RootPath` set to the EFS mount path, `Email__Provider=Ses`, `Email__Region=eu-west-2`, a verified `Email__SenderAddress`, and `Security__TokenCleanupIntervalMinutes` between 5 and 1440 (default 360). Attach the least-privilege SES send policy to the ECS task role. Use `/health/live` for container liveness and `/health/ready` for the load balancer target-group readiness check. Do not configure `Seed__AdminEmail`, `Seed__AdminPassword`, `Seed__DemoPassword`, or `App__LogEmailBodies`; provision administrators separately. The mock payment gateway remains disabled in Production.

For each release:

1. Build and scan the API and frontend images, then push immutable tags to ECR.
2. Take or verify a recent RDS backup and confirm the EFS backup is current.
3. Run one migration task using the new API image and `Database__ApplyMigrationsOnStartup=true`. Stop if it fails.
4. Update the ECS service to the new task definition with `Database__ApplyMigrationsOnStartup=false`. Wait for healthy ALB targets.
5. Smoke-test the public frontend, `/health/live`, `/health/ready`, and a public API route. Verify authenticated SignalR clients can connect through `/hubs/notifications`.
6. Monitor API errors, ALB target health, ECS restarts, and database pressure. Roll back to the prior task definition if the new revision is unhealthy.

Use backward-compatible, additive schema changes while old and new tasks may overlap. For a destructive migration, plan a maintenance window and a tested RDS restore; an application rollback alone cannot reverse destructive data changes.

## Database migration procedure

Do not enable `Database__ApplyMigrationsOnStartup` on a scaled ECS API service. Before a new API release:

1. Run exactly one ECS one-off task using the new image with `Database__ApplyMigrationsOnStartup=true` and the same RDS secret.
2. Wait for it to finish successfully and record the deployment revision.
3. Run the API service with `Database__ApplyMigrationsOnStartup=false`.
4. Roll forward with compatible, additive migrations first. Plan a separate release and backup for destructive changes.

Take automated RDS backups and test restore procedures before storing real customer data. Use UTC in storage; render user-facing appointment times as `Africa/Addis_Ababa` in the client.

## CI/CD minimum gates

- Restore, build and test the backend with Docker available for Testcontainers.
- Run `npm ci`, production build and unit tests for the frontend.
- Build both container images and validate `docker compose config` with injected non-production test values.
- Scan dependencies and images, then deploy an immutable ECR tag only after the above checks pass.

After deployment, verify SES sender identity and sandbox status, then send a password-reset message to a controlled test mailbox.
