# ProgressiveOverload

ProgressiveOverload is a competitive strength-training platform. The tracker is the data
source; the weekly competition is the product.

The current build is the beginning of v1, called **The Loop**. v1 is intentionally small:
a lifter signs up, logs workouts, joins a private lobby, competes for one training week,
and the lobby gets a weekly winner. The point is to prove that a small group of real
lifters wants to come back every week before adding larger systems like XP, seasons,
global leaderboards, feeds, badges, or complex notifications.

## What is happening now

The project is currently in **Milestone 1: Auth & Profile**. The backend foundation is in
place and the first auth slice is implemented.

Already built:

- .NET solution with four projects: `Domain`, `Application`, `Infrastructure`, and `Api`.
- Strict compiler settings through `Directory.Build.props`: nullable enabled and warnings
  treated as errors.
- PostgreSQL 17 local development database through Docker Compose.
- EF Core `AppDbContext`, entity configurations, and the initial migration.
- Domain primitives for `Result`, `Error`, `User`, `BodyweightEntry`, and `RefreshToken`.
- Email/password registration at `POST /api/v1/auth/register`.
- Email/password login at `POST /api/v1/auth/login`.
- JWT access-token creation.
- Opaque refresh-token generation, hashing, persistence, and secure auth cookie handling.
- Integration tests against real PostgreSQL using Testcontainers.

Not built yet:

- Refresh-token rotation endpoint.
- Logout.
- Google sign-in.
- Authenticated profile read/update endpoints.
- Bodyweight recording endpoints.
- Exercise catalog, workout logging, lobbies, scoring, weekly winner finalization, and
  recap email.

The detailed v1 product design lives in
[`docs/superpowers/specs/2026-07-27-the-loop-design.md`](docs/superpowers/specs/2026-07-27-the-loop-design.md).
The current milestone implementation plan lives in
[`docs/superpowers/plans/2026-07-27-m1-auth-and-profile.md`](docs/superpowers/plans/2026-07-27-m1-auth-and-profile.md).

## v1 in plain English

v1 is a private weekly meet for small groups.

Each lobby has a training week. Everyone starts at zero on Monday and competes through
Sunday. The weekly score is based on behavior that should line up with real training:

- showing up for a reasonable number of sessions,
- doing hard enough work sets,
- making personal progress without rewarding fake giant numbers,
- and allowing rest weeks instead of punishing recovery.

The design avoids features that need a large user base. A lobby should be fun with six
people. That is why v1 starts with invite-code lobbies instead of friend graphs, global
leaderboards, divisions, or feeds.

## Architecture

Project references point one way:

```text
Api -> Infrastructure -> Application -> Domain
```

The responsibilities are:

- `ProgressiveOverload.Domain`: dependency-free domain entities and result types.
- `ProgressiveOverload.Application`: feature handlers, validators, DTOs, and EF
  `AppDbContext`.
- `ProgressiveOverload.Infrastructure`: adapters for outside systems such as password
  hashing, JWTs, the current user, and the clock.
- `ProgressiveOverload.Api`: ASP.NET Core minimal API endpoints and HTTP concerns.

There is no repository abstraction over EF Core. Handlers use `AppDbContext` directly,
and `DbContext` lives in `Application` so the reference direction stays clean.

## Local setup

Requirements:

- .NET 10 SDK
- Docker

Start local PostgreSQL:

```bash
docker compose up -d
```

The development database is:

```text
Host=localhost;Port=5434;Database=progressiveoverload;Username=po;Password=localdev
```

Runtime secrets are not committed. For local development, set them with user secrets from
the API project:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5434;Database=progressiveoverload;Username=po;Password=localdev" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:SigningKey" "replace-with-at-least-32-random-bytes" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:Issuer" "progressiveoverload" --project src/ProgressiveOverload.Api
dotnet user-secrets set "Jwt:Audience" "progressiveoverload" --project src/ProgressiveOverload.Api
```

Apply migrations to the local database:

```bash
dotnet ef database update --project src/ProgressiveOverload.Application --startup-project src/ProgressiveOverload.Api
```

Run the API:

```bash
dotnet run --project src/ProgressiveOverload.Api
```

Health check:

```bash
curl http://localhost:5000/health
```

The exact local URL can differ depending on launch settings and environment output from
`dotnet run`.

## Testing

Run everything:

```bash
dotnet test
```

The integration tests do not use the Docker Compose database. They start a separate
throwaway PostgreSQL container with Testcontainers, apply the real migrations, and then run
the API tests against that database.

Current test coverage focuses on:

- `Result` success/failure behavior.
- User creation, Google-linking rules, and bodyweight history rules.
- Refresh-token family, redemption, reuse, expiry, and revocation behavior.
- JWT claims and refresh-token hashing.
- Registration and login HTTP behavior.
- Database schema behavior, including duplicate-email enforcement.

## Current API surface

```text
GET  /health
POST /api/v1/auth/register
POST /api/v1/auth/login
```

Successful register/login responses return an access token in the JSON body and set the
raw refresh token in an HttpOnly, Secure, SameSite=Strict cookie named `po_refresh`. The
raw refresh token is never returned in the response body and is stored only as a SHA-256
hash in PostgreSQL.

## Important project rules

- Weights are stored as decimal kilograms, not floating-point values.
- IDs are UUIDv7.
- Domain stays dependency-free.
- Secrets stay out of git.
- Tests use Shouldly.
- Integration tests use real PostgreSQL.
- User identity must come from the authenticated principal, not a request body or route
  parameter.
