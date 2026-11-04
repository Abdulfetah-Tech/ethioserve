# Authentication

## Overview
- ASP.NET Core Identity (GUID keys) stores users, roles and password hashes.
- Short-lived **access token**: a signed JWT (HMAC-SHA256, 15 minutes). The Angular app keeps it in memory only.
- Long-lived **refresh token**: 64 random bytes, valid 7 days, stored only as a SHA-256 hash.
  It travels in an `HttpOnly; Secure; SameSite=Strict` cookie scoped to `/api/auth` and never appears in a JSON body.

## Access token claims
`sub`, `email`, `jti`, one `role` claim per role, and `customer_id` / `provider_id` when applicable.
Validation: issuer, audience, signature, lifetime (30 s skew) and the algorithm pinned to HS256.
The JWT bearer handler runs with `MapInboundClaims = false` and `RoleClaimType = "role"`.

## Refresh token rotation
Every refresh revokes the used token and issues a new one in the same *family* (one family per login).
Replaying an already-used token revokes the whole family: both the thief and the real user are signed out.
The rotation is claimed with an atomic `UPDATE ... WHERE revoked_at IS NULL`, so concurrent refreshes cannot both succeed.

## Endpoints
| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | /api/auth/register/customer | anonymous | creates user, role, profile and first session in one transaction |
| POST | /api/auth/register/provider | anonymous | provider starts Unverified |
| POST | /api/auth/login | anonymous | generic error for wrong email or password |
| POST | /api/auth/refresh-token | cookie + `X-Requested-With` | rotates the cookie |
| POST | /api/auth/logout | cookie + `X-Requested-With` | revokes the session family |
| POST | /api/auth/forgot-password | anonymous | always 202 |
| POST | /api/auth/reset-password | anonymous | single-use token, revokes all sessions |
| GET | /api/auth/me | bearer | |

## Protections
- Password policy: 10 to 128 characters with lower case, upper case and a digit.
- Lockout: 5 failed attempts lock the account for 15 minutes.
- Rate limit: 10 requests per minute per IP on `/api/auth/*`, 100 per minute globally (configurable under `RateLimiting`).
- CSRF: refresh and logout require the custom `X-Requested-With` header, which a cross-site form cannot send,
  and the cookie is `SameSite=Strict`.
- Default deny: every endpoint requires a logged-in user unless it explicitly opts out with `[AllowAnonymous]`.
- Roles: `Admin`, `Provider`, `Customer`, enforced with policies `AdminOnly`, `ProviderOnly`, `CustomerOnly`.
- Errors never reveal whether an email exists (login, forgot-password).

## Password reset
Link validity is 60 minutes. The token is random, URL-safe and stored only as a hash.
A new request invalidates older links. Resetting also clears lockout and signs the user out everywhere.
Email is sent through `IEmailService`. The development implementation only logs; a real provider is added in Phase 7.

## Known limitations (planned)
- No email verification or multi-factor authentication yet.
- The forgot-password response time differs slightly when the account exists (the email is sent inline).
  Phase 7 moves email sending to a background queue.
- Refresh and reset tokens live in PostgreSQL. A cleanup job for expired rows arrives with the background jobs.
- The development admin comes from local config (`Seed:AdminEmail`, `Seed:AdminPassword`; defaults in `appsettings.Development.json` are `admin@ethioserve.local` / `Admin12345!`). Production admins need a deliberate step.

## Configuration
`Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`, `App:FrontendBaseUrl`,
`App:PasswordResetTokenMinutes`, and the local seed credentials under `Seed:*`.
The signing key is never in a file: use user-secrets locally and the `Jwt__SigningKey` environment variable elsewhere.

## Local development defaults
The repository includes safe local-development defaults so the admin dashboard and demo marketplace are immediately available:

- Admin email: `admin@ethioserve.local`
- Admin password: `Admin12345!`
- Demo user password: `Demo123456!`

These values are only for local development. Do not configure `Seed__AdminEmail`, `Seed__AdminPassword`, or
`Seed__DemoPassword` outside development; provision production administrators through a deliberate, secured procedure.