# ProgressiveOverload — working rules

A competitive strength-training platform. Workout tracking is the data source; the weekly
competition is the product. Solo maintainer.

Product design: `docs/superpowers/specs/2026-07-27-the-loop-design.md`
Current plan: `docs/superpowers/plans/2026-07-27-m1-auth-and-profile.md`

## Architecture

Project references point ONE WAY. Never add one pointing back:

```
Api -> Infrastructure -> Application -> Domain
```

- `Domain` — entities and rules. **Zero package references.** No EF attributes, no
  DataAnnotations, no validation libraries. If code here needs a package, it belongs elsewhere.
- `Application` — feature slices (one folder per operation), plus `AppDbContext`.
- `Infrastructure` — adapters to outside systems: JWT, password hashing, clock, current user.
- `Api` — HTTP only. Minimal APIs. No business logic in endpoints.

**`AppDbContext` lives in `Application`, not `Infrastructure`.** This looks wrong and is
deliberate: there is no repository layer, so handlers use `AppDbContext` directly. Putting it
in `Infrastructure` would make `Application` reference `Infrastructure`, which already
references `Application` — a circular reference that will not compile. `JwtOptions` is in
`Application/Abstractions` for the same reason.

**Do not add a repository abstraction over EF Core.** `DbContext` is already a unit of work
and `DbSet<T>` is already a repository. Wrapping it hides LINQ for no benefit.

## Non-negotiables

- **Weights are `decimal`, stored in kilograms**, `decimal(7,2)` in the database. Never
  `float` or `double` anywhere in the stack. Unit preference is display-only.
- **IDs are UUIDv7** via `Guid.CreateVersion7()`. Time-ordered, so they index well.
- **Persisted enums carry explicit numeric values.** Renaming or reordering a member must
  never change existing rows.
- **Never call `DateTimeOffset.UtcNow` in `Application` code.** Inject `IClock`. Time-dependent
  logic that cannot be tested is the single biggest source of untestable behaviour here.
- **Expected failures return `Result`/`Result<T>`.** Exceptions are for bugs. "Email already
  taken" is a value, not a throw.
- **Zero build warnings.** `TreatWarningsAsErrors` is on, including unused usings.

## Security invariants

Do not weaken these without saying so explicitly:

- **User identity comes from the authenticated principal, never from a request body or route
  parameter.** An endpoint that trusts a caller-supplied `userId` is an IDOR.
- **Membership/ownership is filtered in the query, not just checked at the endpoint.**
  Endpoint checks get forgotten; query filters fail closed.
- **The raw refresh token goes only into the httpOnly cookie.** Never a JSON body, never a log.
  Only its SHA-256 hash is persisted. `AuthResult` keeps it as a separate field from
  `AuthResponse` so it structurally cannot be serialised.
- **Login must not reveal which emails exist.** Unknown email and wrong password return the
  same status, title, and code. And `IPasswordHasher.VerifyDummy` must still run when no user
  matched, so the response takes the same time. Do not add an early return that skips it.
- **Refresh tokens rotate on every use** and inherit their `FamilyId`. Replaying a redeemed
  token means theft: revoke the entire family. In `Redeem`, the reused check comes first —
  reuse must be reported even if the token has since expired or been revoked.
- **Cookie delete options must exactly mirror the set options** (HttpOnly, Secure, SameSite,
  Path). If any differ the browser keeps the cookie and logout silently fails.
- **Migrations are additive only.** Never drop or rename a column in the same deploy that
  stops using it.

## Comments and style

- Use `/* ... */` blocks for explanatory comments on types and members. **Never `///` XML
  doc comments.** Use `//` for short one- or two-line notes inside a method.
- Comment the **why**, not the what. Only where the logic is genuinely non-obvious.
- Dense configuration code (EF mappings, DI wiring, test fixtures) reads as "obvious" to
  someone who knows the library and opaque to everyone else — comment it even though it has
  no branching logic.
- Simplest construct that does the job. No clever LINQ, no nested ternaries, no speculative
  abstraction. The maintainer reads this alone.

## Testing

- **Shouldly for assertions. Never FluentAssertions** — v8+ needs a paid licence for
  commercial use.
- **Integration tests run against real PostgreSQL via Testcontainers.** The EF in-memory
  provider is prohibited: it does not enforce unique indexes or relational constraints, so it
  passes tests that production fails.
- **A test that passes for the wrong reason is worse than no test.** Before trusting a test
  that guards a security property, break the property deliberately and confirm the test fails.
  Several tests in this repo originally passed against a 404.
- **For concurrency tests, report pass/fail per run configuration** — isolated single test,
  filtered class, full suite. A defect here was 100% reproducible in isolation and completely
  invisible when the class ran warm. Never average or generalise across configurations.

## Git

- **Never `git add -A` or `git add .`.** Stage explicit paths. It has twice swept unrelated
  files into commits in this repo.
- Commit messages say what changed and why, and nothing else is in the commit.
- The owner manages the remote and merges PRs himself.

## Local development

```bash
docker compose up -d                                  # Postgres on 5434
dotnet run --project src/ProgressiveOverload.Api
dotnet test
```

**Postgres is on host port 5434.** 5432 is the system default and **5433 belongs to a
different project on this machine** — do not stop, start, or reconfigure that container.

Secrets never enter git. `dotnet user-secrets` locally, environment variables in production.
`Jwt:SigningKey` is validated at startup and must be at least 32 bytes, so the app refuses to
boot without it — that is intentional.
