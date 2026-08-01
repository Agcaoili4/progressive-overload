# ProgressiveOverload

ProgressiveOverload is a competitive strength-training API. The current branch is focused
on **Milestone 1: Auth & Profile**, the backend foundation for v1.

For project working rules, read [CLAUDE.md](CLAUDE.md) first. It is the source of truth for
architecture rules, security invariants, testing expectations, and local workflow.

## Current State

Milestone 1 is mostly implemented. The handoff note from July 30, 2026 says the branch is
11 of 13 tasks complete, with the profile/auth slice working and a small fix round still
open:

- [docs/superpowers/plans/2026-07-30-m1-handoff.md](docs/superpowers/plans/2026-07-30-m1-handoff.md)

Working API endpoints:

```text
GET  /health
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
POST /api/v1/auth/google
GET  /api/v1/me
PATCH /api/v1/me
POST /api/v1/me/bodyweight
```

Implemented so far:

- Email/password registration and login.
- JWT access tokens.
- Opaque refresh tokens stored only as SHA-256 hashes.
- Refresh-token rotation on every use.
- Refresh-token family revocation when reuse is detected.
- Logout with matching cookie-clearing options.
- Google sign-in endpoint and token validator.
- Authenticated profile read/update.
- Authenticated bodyweight logging.
- PostgreSQL schema and EF Core migration for users, bodyweight entries, and refresh tokens.
- Integration tests against real PostgreSQL through Testcontainers.

Still outstanding in Milestone 1:

- Finish the fix round listed in the July 30 handoff.
- Add rate limiting, Serilog, and Sentry.
- Add the CI pipeline.
- Run a final whole-branch review before merge.

Not started yet:

- Exercise catalog.
- Workout logging.
- Lobbies and invite codes.
- Scoring engine and weekly meet finalization.
- Weekly recap email.

## v1 Scope

v1 is called **The Loop**. It is the smallest product slice that can produce a weekly
winner: a user signs up, maintains profile data, logs training, joins a lobby, and competes
for the training week.

The full product design is here:

- [docs/superpowers/specs/2026-07-27-the-loop-design.md](docs/superpowers/specs/2026-07-27-the-loop-design.md)

The active milestone plan is here:

- [docs/superpowers/plans/2026-07-27-m1-auth-and-profile.md](docs/superpowers/plans/2026-07-27-m1-auth-and-profile.md)

## Architecture

Project references point one way:

```text
Api -> Infrastructure -> Application -> Domain
```

Project responsibilities:

- `src/ProgressiveOverload.Domain`: entities, domain rules, result types, and value helpers.
- `src/ProgressiveOverload.Application`: feature handlers, validators, DTOs, and
  `AppDbContext`.
- `src/ProgressiveOverload.Infrastructure`: adapters for password hashing, JWTs, Google
  token validation, current user, and clock.
- `src/ProgressiveOverload.Api`: ASP.NET Core minimal API endpoints, auth middleware, and
  HTTP mapping.

There is no repository abstraction over EF Core. Feature handlers use `AppDbContext`
directly.

## Local Setup

Requirements:

- .NET 10 SDK
- Docker

Start PostgreSQL:

```bash
docker compose up -d
```

Local database:

```text
Host=localhost;Port=5434;Database=progressiveoverload;Username=po;Password=localdev
```

Set local secrets from the API project:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5434;Database=progressiveoverload;Username=po;Password=localdev" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:SigningKey" "replace-with-at-least-32-random-bytes" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:Issuer" "progressiveoverload" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:Audience" "progressiveoverload" --project src/ProgressiveOverload.Api
```

Google sign-in is optional. Without a configured Google client id, the API still starts and
email/password auth still works, but `POST /api/v1/auth/google` fails closed. To enable it:

```bash
dotnet user-secrets set "GoogleAuth:ClientId" "<google-oauth-client-id>" --project src/ProgressiveOverload.Api
```

Apply migrations:

```bash
dotnet ef database update --project src/ProgressiveOverload.Application --startup-project src/ProgressiveOverload.Api
```

Run the API:

```bash
dotnet run --project src/ProgressiveOverload.Api
```

## Testing

Run the full suite:

```bash
dotnet test
```

Integration tests start their own PostgreSQL container with Testcontainers. They do not use
the Docker Compose database.

Current coverage includes:

- Result type behavior.
- Weight conversion.
- User creation, profile rules, Google linking, and bodyweight history.
- Refresh-token issue, rotation, reuse, expiry, revocation, and concurrency behavior.
- JWT creation and Google token validation.
- Register, login, refresh, logout, Google sign-in, profile, and bodyweight endpoints.
- Schema behavior against real PostgreSQL.

## Security Notes

- Raw refresh tokens go only into the `po_refresh` HttpOnly cookie.
- Refresh-token hashes are persisted; raw refresh tokens are not.
- Failed refresh clears the dead cookie.
- Google sign-in validates the Google ID token audience against `GoogleAuth:ClientId`.
- Google sign-in rejects unverified Google emails.
- Authenticated profile/bodyweight handlers read user identity from the validated principal,
  not from request bodies or route parameters.
- JWT issuer, audience, lifetime, and signing key validation are enabled, but the July 30
  handoff calls out missing mutation-proven tests for those validation flags.

## Project Rules

- Weights are decimal kilograms, not floating-point values.
- IDs are UUIDv7.
- Domain has zero package references.
- Expected failures return `Result`/`Result<T>`.
- Secrets stay out of git.
- Tests use Shouldly.
- Integration tests use real PostgreSQL.
