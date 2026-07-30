# ProgressiveOverload — v1 "The Loop" Design

**Date:** 2026-07-27
**Status:** Approved
**Scope:** Sub-project 1 of 6. See [Roadmap](#roadmap).

> **Note for planning:** this spec is one sub-project but still spans four buildable
> milestones — auth & profile, exercise catalog & logging, lobbies & membership, and the
> scoring engine & weekly meet. Expect the implementation plan to sequence them in that
> order, since each depends on the previous one for real test data.

---

## 1. Context and goal

ProgressiveOverload is a competitive strength-training platform. Workout tracking is the
data source; competition is the product.

**v1 success condition:** 20–50 real lifters using it weekly within a couple of months.
Every decision in this document optimizes for shipping something a small group actually
uses — not for architectural completeness and not for scale we do not have.

**v1 is the thinnest end-to-end slice that produces a weekly winner.** Someone signs up,
logs real workouts on their phone, joins a lobby with an invite code, and on Sunday night
one of six people wins the week. If that does not retain 30 people, no amount of XP,
badges, or seasonal ranking will save it. If it does, everything else becomes obvious.

### Open product risk

The first cohort has not been identified or interviewed. This does not block the build —
the design below is robust to all cohort answers — but it remains the highest-value
outstanding action. Interview five prospective users; ask them to *show* how they tracked
their last workout, when they last stopped training consistently and why, and **who they
already compare themselves to**. The third answer is the product. If nobody has one, the
competition thesis needs re-examination.

---

## 2. Product principles

These constrain every future feature decision.

1. **There is no referee.** All input is self-reported and unverifiable. Scoring must be
   cheat-*tolerant* by construction, not cheat-*detected* after the fact.
2. **Nothing that affects competitive outcome will ever be sold.** No XP boosts, no score
   multipliers, no paid streak freezes. The moment money can move a ranking, every ranking
   is meaningless. Monetization may only ever be cosmetic, capacity, or convenience.
3. **The scoring system must not degrade real training.** If the optimal strategy for
   points is bad training, the scoring is wrong.
4. **Rest is never punished.** Real programs deload. A system that penalizes recovery
   harms its users.
5. **Competition must be satisfying at six users in one lobby.** Features requiring
   liquidity we do not have are deferred, not built early.

---

## 3. Scope

### In scope

| Area | Included |
|---|---|
| Auth | Email + password, Google OAuth, refresh tokens with rotation |
| Profile | Display name, avatar, bodyweight, sex, experience level, unit preference |
| Exercises | Seeded catalog (~60 lifts): category, primary muscle group, equipment. Read-only. |
| Logging | Session → sets (exercise, weight, reps). Mobile-first, client-side state, **online-only in v1** (see §8). |
| Strength math | Estimated 1RM per set, best-per-exercise tracking, DOTS relative strength |
| Lobby | Create, join by invite code, member list, rotate/revoke code. Cap 20 members. |
| The Meet | Weekly score, live lobby board, Monday rollover, winner recorded, weeks-won counter |
| Recap | One weekly email: who won, where you placed, what you did |

### Explicitly deferred

- **XP, levels, titles, achievements, badges** — long-loop accumulators. No weeks exist yet
  and each carries a tuning burden that cannot be discharged without data. → Sub-project 2.
- **Friends, friend requests, activity feed** — the lobby *is* the social graph at this
  size. An invite code replaces the entire friendship subsystem. → Sub-project 4.
- **Notification system** — v1 needs exactly one notification (the Sunday recap) and it is
  an email. → Sub-project 4.
- **Comments, reactions, RPE, monthly challenges, global leaderboards, divisions, seasons.**
- **Separate challenge types** — in v1, the weekly meet *is* the weekly challenge.
  → Sub-project 3.
- **Redis, SignalR, background-job framework, soft deletes, audit logging.**

### Load-bearing, do not cut

- Bodyweight and sex on the profile (DOTS requires them).
- Best-per-exercise history (the progression bonus requires it).

---

## 4. The competitive model

### Structure

**The training week is the match.** Every Monday everyone in the lobby starts at zero,
competes through the week, and someone wins on Sunday night. Standings accumulate across
weeks as a weeks-won counter.

This is deliberate: it gives a short, resolvable loop under a long, accumulating one. The
person in fifth place lost *this week*, not forever, which is what keeps them playing. It
also reinforces the Consistency pillar directly.

**Rejected alternative — ranking by absolute or relative strength.** It is static (the
strongest member wins permanently and everyone else disengages), it is the most fakeable
number in the system, and it rewards genetics and training age rather than behavior.

### Weekly score

All values below are **starting guesses to be calibrated against real data**. They live in
versioned configuration, not code.

**Definitions used below**

- A **qualifying set** is one whose **own estimated 1RM is ≥ 80% of the user's baseline
  e1RM** for that exercise. Reps must also fall in [3, 15], but only as a sanity bound —
  Epley becomes unreliable outside that range — not as a scoring rule.
- **Baseline e1RM** is the user's best recorded e1RM for that exercise **as of the moment the
  week opened**, held fixed for the whole week. It must *not* float with mid-week PRs —
  otherwise setting a PR on Tuesday raises the threshold and retroactively disqualifies
  Monday's sets.

  *Why intensity is measured this way.* A flat load threshold (e.g. "≥ 65% of best") admits
  easy sets at low reps — 5 reps at 65% is a warmup, not work — while rejecting genuinely
  hard high-rep sets. Comparing the set's own e1RM to baseline asks the right question: *how
  close to your current capacity was this set?* It behaves correctly at every rep range and
  scales identically for a 315 lb squat and a 25 lb lateral raise.

  *Why not a bodyweight-relative threshold.* Considered and rejected. Bodyweight multiples
  mean radically different things per lift (deadlifting bodyweight is novice, overhead
  pressing it is near-elite), they award zero to any beginner not yet at that standard —
  precisely the user who most needs motivation — and they are meaningless for isolation,
  dumbbell, and machine work. Effort must be measured against the user's own capacity, which
  is what "push yourself to your limits" actually means.
- **Muscle group** means the exercise's `primary_muscle_group` from the catalog. Secondary
  involvement does not count toward caps.
- Sessions are **ordered by `performed_at`** within the week.

**1. Session Points — consistency, ~40% of a typical score**

- Sessions 1–4: 100 pts each
- Sessions 5–6: 40 pts each
- Session 7+: 0 pts
- A session counts only if it contains ≥ 3 qualifying sets.

The diminishing curve prevents "train ten times a week to win," which is both unhealthy and
unfair to anyone with a job. The session floor prevents farming points with a single curl.

**2. Work Points — effort, ~40%**

Score **qualifying sets**, not tonnage.

- 10 pts per qualifying set.
- **Capped at 12 qualifying sets per muscle group per week.** Beyond the cap, 0 pts.

Tonnage is rejected because it rewards heavy people for being heavy and rewards junk volume
linearly. The intensity rule excludes warmups automatically. The per-muscle cap sits roughly
where hypertrophy research places productive weekly volume, so the cap encodes good practice
— you cannot win by doing twenty sets of curls. This is the anti-junk-volume mechanism, and
a cap is preferable to a penalty because it teaches rather than punishes.

**3. Progression Points — improvement, ~20%**

Awarded when a user beats their own previous best estimated 1RM on a lift.

- **Flat value, independent of magnitude.** A +5 lb PR and a +200 lb "PR" pay identically.
- Maximum 3 awards per week, at most one per exercise.
- Compared against the user's best e1RM **at the time the set was performed** (unlike the
  qualifying threshold, which is frozen at week open). This lets a genuine PR register the
  moment it happens while the one-award-per-exercise rule prevents chaining small increments.

The flat-value rule is the primary anti-cheat lever in the design. Under magnitude-based
scoring one fabricated number wins the week outright. Under flat scoring a liar earns the
same three awards an honest lifter earns, so cheating buys almost nothing while costing the
user the thing they came for. **The incentive is removed rather than the behavior detected**
— which is why automated cheat detection can safely remain a future concern.

### Rest weeks

A user may **declare a rest week**: they sit out that week's meet entirely — no score, no
loss, streak preserved. They are shown on the board as resting rather than ranked. Limited to
one per six weeks.

**A rest week must be declared before the week's midpoint (Thursday 00:00 lobby time), and
the declaration is irreversible.** Without that deadline a user could wait until Sunday
night, see they are losing, and opt out of the result — outcome-aware withdrawal. The stakes
are low (there is no loss penalty, only a weeks-won counter) but the loophole is free to
close and expensive to close later.

Considered and rejected: scoring deloads at a trailing median. Sitting out is simpler,
harder to game, and communicates intent more clearly.

### Relative strength (DOTS) is not part of scoring

The weekly score is already fair across bodyweight, sex, and training age by construction:
session points are bodyweight-neutral, work points are measured as a percentage of the
user's *own* e1RM, and progression is measured against the user's *own* history. Adding
DOTS would double-count.

DOTS remains in v1 as a **profile statistic only** — it answers "how strong am I compared to
people in general," serves the Self-Improvement pillar, and gates nothing. Divisions and
weight classes are a global-leaderboard concern (sub-project 5); six people cannot be
bracketed anyway.

### Week boundary rules

- **Weeks belong to the lobby, not the user.** The lobby carries a timezone; the week runs
  Monday 00:00 → Sunday 23:59 in that zone. Per-user boundaries make a shared board
  incoherent — two members would see different weeks on the same screen.
- **Sessions score into the week containing their `performed_at` date**, not their logged-at
  date. Logging Monday's session on Wednesday is normal and must work.
- **A closed week freezes.** Edits to past sessions correct training history but never
  rewrite a finished standing. Otherwise last week's winner can change on Thursday.
- **Grace window:** the week closes Sunday 23:59 but *finalizes* Monday 03:00 lobby time.
  Backdated sessions arriving inside the window still score. After finalization a late
  session is saved to history but does not score, and the API returns a distinct error code
  so the client can say so explicitly. Silent data loss is unrecoverable trust damage.
- **Ties** are broken by total qualifying sets, then by earliest session in the week.

### Scoring configuration is versioned data

Every stored weekly result records the scoring config version that produced it. Retuning is
expected and frequent; old results must stay explicable rather than silently becoming wrong.
This is the difference between a system that can be tuned and one that is frightening to
touch.

---

## 5. Architecture

### The boundary that matters

> **The scoring engine is a pure function:** `(sessions in a week, scoring config) →
> weekly score breakdown`. No database, no clock, no injected services, no I/O.

Consequences: the entire competitive system is unit-testable without a database; any
historical week can be replayed against a candidate config to see what a tuning change
*would have* done before shipping it; property-based testing becomes possible. The most
business-critical and most-frequently-changed code in the product has zero infrastructure
coupling.

### Project layout

```
src/
  ProgressiveOverload.Domain/         # entities, value objects, ScoringEngine. Zero dependencies.
  ProgressiveOverload.Application/    # feature slices: handlers, DTOs, validators
  ProgressiveOverload.Infrastructure/ # EF Core, DbContext, migrations, email, auth
  ProgressiveOverload.Api/            # minimal API endpoints, DI wiring, middleware
tests/
  ProgressiveOverload.Domain.Tests/
  ProgressiveOverload.Integration.Tests/
```

Application code is organized **by feature, not by type**:

```
Application/Workouts/LogSession/{Command,Handler,Validator,Response}.cs
Application/Lobbies/JoinByInviteCode/{...}
Application/Meets/GetLobbyBoard/{...}
```

### Deviations from textbook Clean Architecture, with reasons

- **No repository layer over EF Core.** `DbContext` is already a unit of work and `DbSet<T>`
  is already a repository. Wrapping it hides LINQ, makes efficient queries awkward, and buys
  a database-swap capability that will never be exercised.
- **Minimal APIs, not controllers.** They compose with vertical slices; controllers accrete
  into god-classes.
- **No MediatR initially.** Its payoff is pipeline behaviors and cross-team decoupling; at
  one developer and ~20 slices it is indirection paid for on every stack frame. Handlers are
  injected directly. Adding it later is mechanical.
- **Result pattern for domain failures, exceptions for bugs.** "Invite code expired" is a
  `Result.Failure`; exceptions are reserved for genuinely exceptional states.

---

## 6. Data model

Core aggregates: **User/Profile**, **Exercise**, **WorkoutSession → WorkoutSet**,
**ExerciseBest**, **Lobby → LobbyMembership**, **LobbyWeek → WeeklyScore**, **ScoringConfig**.

### Decisions that are non-obvious or expensive to reverse

**Weight is `decimal(7,2)`, stored canonically in kilograms.** Never floating point. Unit
preference lives on the profile and applies to display only. Mixed-unit storage produces
bugs that surface months later in leaderboard disputes; float rounding makes PRs flicker.

**The API speaks kilograms only; conversion happens at the display edge.** A number in a
request is then never ambiguous, and comparisons, personal records, and the scoring engine
all operate on one scale.

"Display" is not purely a frontend concern, though — **the weekly recap email is
server-generated**, so the server needs conversion too. `Domain/Common/WeightConversion.cs`
provides it, deriving both directions from the exact definition (one pound = 0.45359237 kg)
so they cannot drift apart.

Display rounding is a product rule, not a formatting detail. Metric rounds to one decimal
place. **Imperial snaps to the nearest 0.5 lb**, because a lifter who enters 225 lb has it
stored as 102.06 kg and a naive conversion back reads 224.99 lb — which looks like a bug to
someone who knows they lifted 225. Half-pound steps restore the entered number while still
representing micro-plate loads like 227.5 lb. Both directions round away from zero; .NET's
default banker's rounding would send a value sitting exactly on a boundary to the wrong one.

**Bodyweight is a time series.** DOTS requires bodyweight *at the time of the lift*, and
users want the trend. A `bodyweight_entries` table, plus a denormalized `bodyweight_kg`
snapshot copied onto each session at creation so sessions stay explicable years later.

**Client-generated UUIDv7 for sessions and sets.** Load-bearing for the offline plan: when a
phone syncs a session recorded without signal, the request may retry, duplicate, or arrive
out of order. Client-owned IDs make the write naturally idempotent — an upsert on a known
primary key. UUIDv7 specifically because it is time-ordered and therefore indexes well in
Postgres rather than fragmenting like UUIDv4.

**`ExerciseBest` is a derived cache and must be rebuildable.** Best e1RM per user per
exercise, maintained incrementally on write, with a rebuild-from-sets path. Any derived table
that cannot be reconstructed is a permanent data-integrity risk.

**`LobbyWeek` is a real entity, not a query.** Fields: `lobby_id`, `starts_at`, `ends_at`,
`finalize_at`, `status` (`open` | `finalizing` | `final`), `scoring_config_version`. On
finalization it stores each member's **component values**, not just a total — "why did I get
340 points that week" must be answerable without re-running anything.

**The e1RM formula is recorded per set.** Epley, Brzycki, and Lombardi disagree by up to ~5%.
v1 uses **Epley** for simplicity and reasonable sub-10-rep behavior, and stores which formula
produced each value — same reasoning as versioned scoring config.

**Invite codes are a security surface.** ~8 characters from an unambiguous alphabet
(excluding `0`/`O` and `1`/`I`/`l`), generated from a CSPRNG, rate-limited at the join
endpoint, rotatable and revocable. Predictable codes mean strangers in private lobbies.

### Indexes required at launch

- `workout_sessions (user_id, performed_at desc)` — week query and history feed
- `workout_sets (session_id)`
- `workout_sets (user_id, exercise_id, performed_at desc)` — progression charts, best rebuild
- `lobby_memberships (lobby_id, user_id)` unique; `lobby_memberships (user_id)`
- `lobbies (invite_code)` unique
- `weekly_scores (lobby_week_id, total_points desc)` — the board render, the hottest read

### Deliberate omissions

No soft deletes (hard deletes, guarded against finalized weeks) and no audit log beyond
`created_at` / `updated_at`. Both are easy to add and neither earns its keep at 30 users.

---

## 7. Authentication and authorization

### Authentication

**ASP.NET Core Identity for the user store and password hashing only**; tokens are issued
directly rather than using Identity's cookie stack, because the API must serve web today and
mobile later.

- **Access token:** JWT, 10–15 minute lifetime, minimal claims (subject, token version).
  Nothing authorization-relevant that can go stale.
- **Refresh token:** opaque random value (not a JWT), stored **hashed**, **rotated on every
  use**, with **reuse detection** — presenting an already-redeemed token revokes the entire
  token family and forces re-login. This is the only reliable signal that a token was stolen.
- **Browser storage:** refresh token in an `httpOnly; Secure; SameSite` cookie; access token
  in memory only. Never `localStorage` — any XSS anywhere turns it into permanent account
  handover.

**Infrastructure consequence:** same-site cookies require the API to be same-site with the
web app. Serve the app from `progressiveoverload.app` and the API from
`api.progressiveoverload.app`. Leaving the API on `*.onrender.com` forces `SameSite=None`,
weakens the cookie, and drags in CORS-with-credentials and CSRF handling. Custom domain on
the API from day one.

**Google OAuth:** authorization code flow with PKCE. When linking a Google identity to an
existing email account, **the `email_verified` claim must be checked** — auto-linking on
unverified email is a straightforward account takeover.

**Passwords:** NIST guidance — minimum 12 characters, no composition rules, no expiry.

**Rate limiting** via the built-in .NET limiter: aggressive on login, registration, password
reset, and **invite-code join** (an enumeration target); generous per-user limits elsewhere.

### Authorization

Almost entirely **resource-based, not role-based** — the question is nearly always "is this
user a member of this lobby?" Implemented with ASP.NET resource-based handlers and two
policies: `LobbyMember` and `LobbyOwner`.

Two rules:

1. **User identity comes from the token, never from the request.** Any endpoint trusting a
   `userId` from a body or route is an IDOR.
2. **Membership is enforced at the query, not only at the endpoint.** Endpoint checks get
   forgotten on the twentieth slice; query-level filtering fails closed.

---

## 8. API surface

Versioned under `/api/v1`. RFC 7807 `ProblemDetails` for every error, so the client handles
one error shape.

```
POST   /auth/register · /auth/login · /auth/refresh · /auth/logout · /auth/google
GET    /me · PATCH /me · POST /me/bodyweight
GET    /exercises                       (cacheable, rarely changes)
PUT    /workouts/sessions/{id}          (idempotent upsert — the sync endpoint)
GET    /workouts/sessions               (paginated history)
DELETE /workouts/sessions/{id}
GET    /me/exercises/{id}/progress      (e1RM over time)
POST   /lobbies · POST /lobbies/join · GET /lobbies/{id}
POST   /lobbies/{id}/invite-code/rotate
GET    /lobbies/{id}/weeks/current      (the board)
GET    /lobbies/{id}/weeks/{weekId}     (a finalized week)
POST   /lobbies/{id}/weeks/current/rest (declare a rest week)
```

### Sync contract

`PUT /workouts/sessions/{clientGeneratedId}` accepts the **whole session including all sets**
and upserts it. The client owns the ID, so retries are idempotent; the server returns
canonical state for the client to reconcile against.

**Conflict resolution is last-write-wins on `updated_at`.** This is a deliberate
simplification. Proper multi-device convergence means CRDTs, and CRDTs are a project. The
real-world case — one person, one phone, one session — never conflicts. The rare case,
simultaneous edits from a laptop and a phone, loses one edit. Acceptable at this scale, and
recorded here rather than pretended away.

### Client architecture

Logging is a **mobile-first, client-side app with a local store**, shipping **online-only in
v1**. Offline sync is deferred (sub-project 6) but the client is built client-heavy *now*,
because retrofitting offline onto a server-rendered form is a rewrite whereas adding a sync
layer beneath an existing client store is an addition.

---

## 9. The week rollover job

The only genuinely tricky infrastructure in v1, and the piece that must not fail.

A plain **`BackgroundService`** in the API process ticking every ~5 minutes, claiming due
weeks with:

```sql
SELECT ... FROM lobby_weeks
WHERE status = 'open' AND finalize_at <= now()
FOR UPDATE SKIP LOCKED
```

Postgres is the job coordinator at this scale — no Hangfire, Quartz, or queue. The job is
**idempotent**: finalizing an already-final week is a no-op, so a crash mid-run is harmless.
On finalization, the next `LobbyWeek` is created eagerly.

**Finalize and notify are separate concerns.** Computing and storing scores is one
transaction; sending recap emails is a separate retryable step with its own status. An email
outage must never roll back a finalized week or cause double finalization.

**Deployment gotcha:** Render's free tier spins down idle instances, and a sleeping instance
runs no background jobs — weeks would finalize whenever someone happens to visit. Use a paid
always-on instance (~$7/mo, the recommended option), or drive the tick from a GitHub Actions
scheduled workflow hitting a secret-authenticated endpoint.

---

## 10. Infrastructure

**Vercel (web) + Render (API, paid starter) + Neon (Postgres).** Three vendors.

**Dropped from the original stack for v1: Redis/Upstash and SignalR.** At 30–50 users on a
single instance, in-memory caching covers the exercise catalog, the built-in .NET rate
limiter partitions in memory, and a leaderboard that refreshes on page load is
indistinguishable from a live one. Redis adds a vendor, a connection that can fail, and a
second source of truth, for no measurable benefit. Both are additive changes when multiple
instances or a genuinely hot leaderboard read arrive.

Operational notes:

- Use **Neon's pooled connection string** with EF Core; direct connections will exhaust.
- Neon's free tier scales to zero — the first request after idle is slow.
- Transactional email via **Resend** or **Postmark**.
- Local development: Docker Compose running Postgres only.
- Secrets: Render environment variables in production, `dotnet user-secrets` locally. Never
  in git.

---

## 11. Observability

**Serilog** structured logging to console (Render captures it) with request correlation IDs,
plus **Sentry** for exceptions. Application Insights and Seq are skipped — hosted
observability before traffic exists is optimizing an empty room.

Sentry earns its place on three failure modes that are otherwise invisible:

1. **The rollover job failing silently.** If finalization throws at 03:00 Monday, nothing
   else tells you; you find out when a user reports a stale board. A **dedicated alert rule
   on this job** is required.
2. **Sync failures from real phones.** A swallowed sync error loses a user's logged work, and
   they will not file a bug — they will stop using the app.
3. **Gym-floor browser bugs** on devices we cannot reproduce.

Configuration:

- `Sentry.AspNetCore` and `@sentry/nextjs`.
- **Release tagging from the git SHA in CI**, so every error identifies its deploy.
- **Source maps uploaded** for the frontend, or traces are unreadable.
- **`SendDefaultPii = false` and request-body scrubbing.** The app handles email addresses
  and **bodyweight** — health-adjacent personal data that must not be shipped to a
  third-party error tracker in a payload.
- Performance tracing sampled at 5–10%; Sentry disabled entirely in local development.

Free tier (5k events/month) is sufficient.

---

## 12. Monetization

**v1: a Ko-fi or GitHub Sponsors link in the footer. No payment integration.**

Stripe integration means Checkout, webhooks, subscription state, failed payments,
cancellations, refunds, receipts, tax handling, and a Terms of Service — roughly a week of
work plus permanent maintenance obligation, in exchange for revenue from thirty people, at
the cost of attention on the only v1 question that matters.

**The ask is not shown until a user has completed four weeks.** Requesting money before
delivering value reads as a hobby project.

**Doctrine (see Principle 2): nothing affecting competitive outcome is ever sold.** This is
the same principle as the flat-value PR rule — the incentive to distort rankings is removed
rather than policed. Selling advantage reintroduces exactly what the scoring design removed,
except self-inflicted.

Permitted monetization when the time comes, in order of fit:

1. **Cosmetic** — profile flair, lobby themes, animated badges, supporter markers.
2. **Capacity** — larger lobbies, longer history retention, more concurrent lobbies.
3. **Convenience / insight** — data export, deep analytics, program templates.

None require anything in the v1 schema.

---

## 13. Testing

- **Domain tests are the bulk** — fast, pure, no database, concentrated on the scoring engine.
- **Property-based tests on scoring** (CsCheck or FsCheck): score is never negative; adding a
  set never decreases score; the per-muscle cap always binds; a rest week never lowers
  standing.
- **Golden-file tests for scoring** — fixture weeks with expected breakdowns committed to the
  repo. When config is retuned, the diff shows exactly what changed for real-shaped weeks.
  This is what makes tuning safe rather than frightening.
- **Integration tests against real Postgres via Testcontainers.** The EF in-memory provider
  is prohibited — it does not enforce constraints or model relational behavior, so it passes
  tests that production fails.
- **A handful of Playwright E2E:** register → log a session → join a lobby → see the board.

---

## 14. Delivery

**Branching:** trunk-based on `main` with short-lived branches and self-reviewed PRs. The PR
exists so CI runs before merge. GitFlow is ceremony that buys a solo developer nothing.

**CI (on PR):** build, format check, unit tests, integration tests against real Postgres via
Testcontainers.

**CD (on merge to `main`):** migrate, then deploy.

**Migration discipline — additive only.** Never drop or rename a column in the same deploy
that stops using it; expand first, deploy, contract in a later release. Otherwise a rollback
leaves running code pointed at a schema that no longer matches and there is no way back.

---

## Roadmap

1. **The Loop** — this specification
2. **Progression** — XP, levels, titles, achievements, badges, calibrated against real week data
3. **Challenges & lobby mechanics** — weekly/monthly challenge types, co-op goals, head-to-head rivalries
4. **Social** — friends beyond lobbies, activity feed, notifications, reactions
5. **Public competition** — global leaderboards, DOTS divisions, seasons
6. **Offline hardening** — sync layer beneath the client store; native app if warranted

### On scaling

Nothing here blocks large scale, and nothing here is built for it. The two components that
break first are leaderboard reads and the finalize job; both have well-understood fixes (a
materialized board, sharded or queued finalization) to be applied when the numbers demand
them. Building them now would cost weeks and buy nothing measurable. The likeliest cause of
failure is that thirty people do not care, and no scale architecture addresses that.
