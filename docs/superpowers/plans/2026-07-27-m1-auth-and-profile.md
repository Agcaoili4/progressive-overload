# Milestone 1: Auth & Profile — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A deployed, tested ASP.NET Core API where a real person can register with email/password or Google, stay signed in across sessions, and manage their profile and bodyweight history.

**Architecture:** Four-project solution — `Domain` (pure, zero dependencies), `Application` (feature slices), `Infrastructure` (EF Core, auth, email), `Api` (minimal API endpoints). Vertical slices organized by feature, not by technical layer. No repository abstraction over EF Core; `DbContext` is the unit of work. Domain failures return `Result`, exceptions are reserved for bugs.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10 + Npgsql, PostgreSQL 17, xUnit + Shouldly + Testcontainers, FluentValidation, Serilog, Sentry.

**Spec:** [docs/superpowers/specs/2026-07-27-the-loop-design.md](../specs/2026-07-27-the-loop-design.md) — §5 architecture, §6 data model, §7 auth/authz, §8 API surface, §11 observability, §13 testing, §14 delivery.

## Global Constraints

- **.NET 10** (current LTS). `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` on every project.
- **Weights are `decimal(7,2)`, stored canonically in kilograms.** Floating-point types for weight are prohibited anywhere in the stack.
- **IDs are UUIDv7**, generated with `Guid.CreateVersion7()`. Client-supplied for workout sessions and sets (Milestone 2); server-generated elsewhere.
- **Use Shouldly, not FluentAssertions**, for test assertions. FluentAssertions v8+ requires a paid licence for commercial use, and this is a commercial project.
- **No repository interfaces over EF Core.** Handlers take `AppDbContext` directly.
- **Domain project has zero package references.** If a task tempts you to add one, the code belongs in `Application` or `Infrastructure`.
- **Secrets never enter git.** `dotnet user-secrets` locally, environment variables in production.
- **Migrations are additive only.** Never drop or rename a column in the same deploy that stops using it.
- **User identity comes from the authenticated principal, never from a request body or route parameter.**

---

## File Structure

```
ProgressiveOverload.sln
Directory.Build.props                  # shared compiler settings for all projects
docker-compose.yml                     # Postgres 17 only
.editorconfig

src/
  ProgressiveOverload.Domain/
    Common/Result.cs                   # Result / Result<T> discriminated result type
    Common/Error.cs                    # code + message pair
    Users/User.cs                      # aggregate root: identity + profile
    Users/Sex.cs                       # enum, required for DOTS
    Users/ExperienceLevel.cs           # enum
    Users/UnitPreference.cs            # enum, display only
    Users/BodyweightEntry.cs           # time-series entity
    Users/UserErrors.cs                # named domain errors for the Users slice
    Auth/RefreshToken.cs               # hashed, rotating, family-tracked
    Auth/AuthErrors.cs

  ProgressiveOverload.Application/
    Abstractions/IPasswordHasher.cs    # port, implemented in Infrastructure
    Abstractions/ITokenService.cs
    Abstractions/ICurrentUser.cs
    Abstractions/IGoogleTokenValidator.cs
    Abstractions/IClock.cs             # never call DateTimeOffset.UtcNow directly
    Abstractions/JwtOptions.cs         # configuration contract, not an infra detail
    Persistence/AppDbContext.cs
    Persistence/Configurations/{UserConfiguration,BodyweightEntryConfiguration,RefreshTokenConfiguration}.cs
    Persistence/Migrations/            # EF-generated
    Users/Register/{RegisterCommand,RegisterHandler,RegisterValidator}.cs
    Users/Login/{LoginCommand,LoginHandler,LoginValidator}.cs
    Users/Refresh/{RefreshCommand,RefreshHandler}.cs
    Users/Logout/{LogoutCommand,LogoutHandler}.cs
    Users/GoogleSignIn/{GoogleSignInCommand,GoogleSignInHandler}.cs
    Users/GetProfile/{GetProfileQuery,GetProfileHandler,ProfileResponse}.cs
    Users/UpdateProfile/{UpdateProfileCommand,UpdateProfileHandler,UpdateProfileValidator}.cs
    Users/RecordBodyweight/{RecordBodyweightCommand,RecordBodyweightHandler,RecordBodyweightValidator}.cs

  ProgressiveOverload.Infrastructure/  # adapters to external systems only
    Auth/PasswordHasherAdapter.cs      # wraps Microsoft.AspNetCore.Identity.PasswordHasher
    Auth/JwtTokenService.cs
    Auth/GoogleTokenValidator.cs
    Auth/CurrentUser.cs
    Time/SystemClock.cs
    DependencyInjection.cs

  ProgressiveOverload.Api/
    Program.cs
    Endpoints/AuthEndpoints.cs
    Endpoints/ProfileEndpoints.cs
    Extensions/ResultExtensions.cs     # Result -> IResult / ProblemDetails
    Extensions/ValidationExtensions.cs
    Middleware/ExceptionHandler.cs
    appsettings.json

tests/
  ProgressiveOverload.Domain.Tests/
    Users/UserTests.cs
    Auth/RefreshTokenTests.cs
    Common/ResultTests.cs
  ProgressiveOverload.Integration.Tests/
    Infrastructure/PostgresFixture.cs  # Testcontainers, shared across the collection
    Infrastructure/ApiFactory.cs       # WebApplicationFactory wired to the container
    Auth/RegisterTests.cs
    Auth/LoginTests.cs
    Auth/RefreshRotationTests.cs
    Profile/ProfileTests.cs
    Profile/BodyweightTests.cs

.github/workflows/ci.yml
```

**Why `AppDbContext` lives in `Application`, not `Infrastructure`.** Project references point one way only: `Api → Infrastructure → Application → Domain`. Spec §5 rules out a repository layer, so handlers use `AppDbContext` directly — which means if the context lived in `Infrastructure`, `Application` would have to reference it, producing a reference cycle that does not compile. Since we are deliberately not abstracting EF Core, `DbContext` *is* the application's data-access API and belongs beside the handlers that use it. `Infrastructure` keeps what it should: adapters to genuinely external systems (JWT signing, password hashing, Google, the clock) plus DI wiring.

**Why `User` holds profile data instead of a separate `Profile` entity.** The spec's entity list names them separately. A separate table earns its keep when the relationship is optional, versioned, or independently permissioned — none apply here. It is 1:1, mandatory, and always read together, so splitting it buys a join on every request and nothing else. Bodyweight is the one profile attribute that *is* genuinely temporal, and it gets its own table for that reason.

---

## Task 1: Solution scaffold and test harness

**Files:**
- Create: `ProgressiveOverload.sln`, `Directory.Build.props`, `docker-compose.yml`, `.editorconfig`, `.gitignore`
- Create: all six project files
- Test: `tests/ProgressiveOverload.Domain.Tests/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing
- Produces: a solution where `dotnet test` runs green, and a Postgres container on `localhost:5433`

- [ ] **Step 1: Create the solution and projects**

```bash
dotnet new sln -n ProgressiveOverload
dotnet new classlib -o src/ProgressiveOverload.Domain -f net10.0
dotnet new classlib -o src/ProgressiveOverload.Application -f net10.0
dotnet new classlib -o src/ProgressiveOverload.Infrastructure -f net10.0
dotnet new web -o src/ProgressiveOverload.Api -f net10.0
dotnet new xunit -o tests/ProgressiveOverload.Domain.Tests -f net10.0
dotnet new xunit -o tests/ProgressiveOverload.Integration.Tests -f net10.0
dotnet sln add $(find src tests -name "*.csproj")

dotnet add src/ProgressiveOverload.Application reference src/ProgressiveOverload.Domain
dotnet add src/ProgressiveOverload.Infrastructure reference src/ProgressiveOverload.Application
dotnet add src/ProgressiveOverload.Api reference src/ProgressiveOverload.Infrastructure
dotnet add tests/ProgressiveOverload.Domain.Tests reference src/ProgressiveOverload.Domain
dotnet add tests/ProgressiveOverload.Integration.Tests reference src/ProgressiveOverload.Api

rm src/ProgressiveOverload.Domain/Class1.cs src/ProgressiveOverload.Application/Class1.cs src/ProgressiveOverload.Infrastructure/Class1.cs
```

Note the reference direction: `Api → Infrastructure → Application → Domain`. Nothing points back. If you ever need to add a reference in the other direction, the code is in the wrong project.

- [ ] **Step 2: Add shared compiler settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Add the local Postgres container**

Create `docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: po
      POSTGRES_PASSWORD: localdev
      POSTGRES_DB: progressiveoverload
    ports:
      - "5433:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U po -d progressiveoverload"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  pgdata:
```

Port 5433 deliberately, so this never collides with a Postgres you already have on 5432. No Redis service — see spec §10.

- [ ] **Step 4: Add Shouldly and write a smoke test**

```bash
dotnet add tests/ProgressiveOverload.Domain.Tests package Shouldly
```

Create `tests/ProgressiveOverload.Domain.Tests/SmokeTest.cs`:

```csharp
using Shouldly;

namespace ProgressiveOverload.Domain.Tests;

public class SmokeTest
{
    [Fact]
    public void TestHarnessRuns()
    {
        (2 + 2).ShouldBe(4);
    }
}
```

- [ ] **Step 5: Verify the harness**

Run: `docker compose up -d && dotnet build && dotnet test`
Expected: build succeeds with zero warnings, 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, compiler settings, and local Postgres"
```

---

## Task 2: The Result type

**Files:**
- Create: `src/ProgressiveOverload.Domain/Common/Error.cs`, `src/ProgressiveOverload.Domain/Common/Result.cs`
- Test: `tests/ProgressiveOverload.Domain.Tests/Common/ResultTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `Error(string Code, string Message)`; `Result` with `.Success()`, `.Failure(Error)`, `.IsSuccess`, `.Error`; `Result<T>` with `.Success(T)`, `.Failure(Error)`, `.Value`. Every handler in this plan returns one of these.

- [ ] **Step 1: Write the failing tests**

Create `tests/ProgressiveOverload.Domain.Tests/Common/ResultTests.cs`:

```csharp
using ProgressiveOverload.Domain.Common;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();
        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var error = new Error("users.email_taken", "That email is already registered.");
        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void AccessingValueOnFailure_Throws()
    {
        var result = Result<int>.Failure(new Error("x", "y"));
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }
}
```

That last test matters: reading `.Value` off a failed result is a programming error, and it should fail loudly at the point of the mistake rather than silently returning `default`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter ResultTests`
Expected: FAIL — `Result` and `Error` do not exist.

- [ ] **Step 3: Implement Error and Result**

Create `src/ProgressiveOverload.Domain/Common/Error.cs`:

```csharp
namespace ProgressiveOverload.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

Create `src/ProgressiveOverload.Domain/Common/Result.cs`:

```csharp
namespace ProgressiveOverload.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot carry an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must carry an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value from a failed result.");

    public static Result<T> Success(T value) => new(value, true, Error.None);
    public static new Result<T> Failure(Error error) => new(default, false, error);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter ResultTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/ProgressiveOverload.Domain/Common tests/ProgressiveOverload.Domain.Tests/Common
git commit -m "feat(domain): add Result and Error types"
```

---

## Task 3: User aggregate and profile enums

**Files:**
- Create: `src/ProgressiveOverload.Domain/Users/{Sex,ExperienceLevel,UnitPreference,User,BodyweightEntry,UserErrors}.cs`
- Test: `tests/ProgressiveOverload.Domain.Tests/Users/UserTests.cs`

**Interfaces:**
- Consumes: `Result`, `Error` (Task 2)
- Produces:
  - `enum Sex { Male = 1, Female = 2 }`
  - `enum ExperienceLevel { Beginner = 1, Novice = 2, Intermediate = 3, Advanced = 4 }`
  - `enum UnitPreference { Metric = 1, Imperial = 2 }`
  - `User.CreateWithPassword(string email, string passwordHash, string displayName) -> Result<User>`
  - `User.CreateFromGoogle(string email, string googleSubject, string displayName) -> Result<User>`
  - `user.LinkGoogleAccount(string googleSubject) -> Result`
  - `user.UpdateProfile(string displayName, string? bio, Sex? sex, ExperienceLevel? level, UnitPreference units) -> Result`
  - `user.RecordBodyweight(decimal kg, DateTimeOffset at) -> Result<BodyweightEntry>`
  - Properties: `Id`, `Email`, `PasswordHash`, `GoogleSubject`, `DisplayName`, `Bio`, `AvatarUrl`, `Sex`, `ExperienceLevel`, `Units`, `CurrentBodyweightKg`, `CreatedAt`, `SecurityStamp`

- [ ] **Step 1: Write the failing tests**

Create `tests/ProgressiveOverload.Domain.Tests/Users/UserTests.cs`:

```csharp
using ProgressiveOverload.Domain.Users;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Users;

public class UserTests
{
    private static User AValidUser() =>
        User.CreateWithPassword("lifter@example.com", "hash", "Jansen").Value;

    [Fact]
    public void CreateWithPassword_NormalisesEmailToLowercase()
    {
        var user = User.CreateWithPassword("Lifter@Example.COM", "hash", "Jansen").Value;
        user.Email.ShouldBe("lifter@example.com");
    }

    [Fact]
    public void CreateWithPassword_AssignsTimeOrderedId()
    {
        var first = AValidUser();
        var second = AValidUser();
        second.Id.CompareTo(first.Id).ShouldNotBe(0);
        first.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CreateWithPassword_RejectsBlankDisplayName()
    {
        var result = User.CreateWithPassword("a@b.com", "hash", "   ");
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.DisplayNameRequired);
    }

    [Fact]
    public void CreateFromGoogle_HasNoPasswordHash()
    {
        var user = User.CreateFromGoogle("a@b.com", "google-sub-123", "Jansen").Value;
        user.PasswordHash.ShouldBeNull();
        user.GoogleSubject.ShouldBe("google-sub-123");
    }

    [Fact]
    public void LinkGoogleAccount_FailsIfAlreadyLinkedToDifferentSubject()
    {
        var user = AValidUser();
        user.LinkGoogleAccount("sub-1").IsSuccess.ShouldBeTrue();

        var result = user.LinkGoogleAccount("sub-2");
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.GoogleAlreadyLinked);
    }

    [Fact]
    public void RecordBodyweight_RejectsImplausibleValues()
    {
        var user = AValidUser();
        user.RecordBodyweight(19m, DateTimeOffset.UtcNow).IsFailure.ShouldBeTrue();
        user.RecordBodyweight(501m, DateTimeOffset.UtcNow).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void RecordBodyweight_UpdatesCurrentBodyweight()
    {
        var user = AValidUser();
        var entry = user.RecordBodyweight(84.5m, DateTimeOffset.UtcNow).Value;

        entry.WeightKg.ShouldBe(84.5m);
        user.CurrentBodyweightKg.ShouldBe(84.5m);
    }

    [Fact]
    public void RecordBodyweight_OlderEntryDoesNotOverwriteCurrent()
    {
        var user = AValidUser();
        var now = DateTimeOffset.UtcNow;

        user.RecordBodyweight(84.5m, now);
        user.RecordBodyweight(90m, now.AddDays(-30));

        user.CurrentBodyweightKg.ShouldBe(84.5m);
    }
}
```

That final test is the one worth having. Backfilling old bodyweight entries is a normal thing for a user to do, and a naive implementation would set their current weight to a value from a month ago — which would then feed DOTS and every session snapshot in Milestone 2.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter UserTests`
Expected: FAIL — `User` does not exist.

- [ ] **Step 3: Implement the enums**

Create `src/ProgressiveOverload.Domain/Users/Sex.cs`:

```csharp
namespace ProgressiveOverload.Domain.Users;

/// <summary>
/// Required by the DOTS relative-strength formula, which is defined only for these
/// two coefficient sets. Stored separately from any identity concept.
/// </summary>
public enum Sex
{
    Male = 1,
    Female = 2
}
```

Create `src/ProgressiveOverload.Domain/Users/ExperienceLevel.cs`:

```csharp
namespace ProgressiveOverload.Domain.Users;

public enum ExperienceLevel
{
    Beginner = 1,
    Novice = 2,
    Intermediate = 3,
    Advanced = 4
}
```

Create `src/ProgressiveOverload.Domain/Users/UnitPreference.cs`:

```csharp
namespace ProgressiveOverload.Domain.Users;

/// <summary>Display only. All weights are persisted in kilograms.</summary>
public enum UnitPreference
{
    Metric = 1,
    Imperial = 2
}
```

Explicit numeric values on all three: these are persisted, and letting the compiler renumber them when someone alphabetises the members would silently rewrite everyone's data.

- [ ] **Step 4: Implement the domain errors**

Create `src/ProgressiveOverload.Domain/Users/UserErrors.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailRequired =
        new("users.email_required", "An email address is required.");

    public static readonly Error DisplayNameRequired =
        new("users.display_name_required", "A display name is required.");

    public static readonly Error DisplayNameTooLong =
        new("users.display_name_too_long", "Display name must be 30 characters or fewer.");

    public static readonly Error EmailAlreadyRegistered =
        new("users.email_already_registered", "That email is already registered.");

    public static readonly Error GoogleAlreadyLinked =
        new("users.google_already_linked", "This account is already linked to a different Google account.");

    public static readonly Error ImplausibleBodyweight =
        new("users.implausible_bodyweight", "Bodyweight must be between 20 kg and 500 kg.");

    public static readonly Error NotFound =
        new("users.not_found", "User not found.");
}
```

- [ ] **Step 5: Implement BodyweightEntry and User**

Create `src/ProgressiveOverload.Domain/Users/BodyweightEntry.cs`:

```csharp
namespace ProgressiveOverload.Domain.Users;

public sealed class BodyweightEntry
{
    private BodyweightEntry() { } // EF Core

    internal BodyweightEntry(Guid userId, decimal weightKg, DateTimeOffset recordedAt)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        WeightKg = weightKg;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal WeightKg { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
}
```

Create `src/ProgressiveOverload.Domain/Users/User.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Users;

public sealed class User
{
    public const decimal MinBodyweightKg = 20m;
    public const decimal MaxBodyweightKg = 500m;
    public const int MaxDisplayNameLength = 30;

    private readonly List<BodyweightEntry> _bodyweightEntries = [];

    private User() { } // EF Core

    private User(string email, string? passwordHash, string? googleSubject, string displayName)
    {
        Id = Guid.CreateVersion7();
        Email = email;
        PasswordHash = passwordHash;
        GoogleSubject = googleSubject;
        DisplayName = displayName;
        Units = UnitPreference.Metric;
        SecurityStamp = Guid.CreateVersion7();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public string? GoogleSubject { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Sex? Sex { get; private set; }
    public ExperienceLevel? ExperienceLevel { get; private set; }
    public UnitPreference Units { get; private set; }
    public decimal? CurrentBodyweightKg { get; private set; }
    public DateTimeOffset? CurrentBodyweightAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Rotated on password change or global sign-out. Embedded in access tokens so that
    /// already-issued JWTs can be invalidated without a token blacklist.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    public IReadOnlyList<BodyweightEntry> BodyweightEntries => _bodyweightEntries;

    public static Result<User> CreateWithPassword(string email, string passwordHash, string displayName)
    {
        var validation = ValidateCore(email, displayName);
        if (validation.IsFailure) return Result<User>.Failure(validation.Error);

        return Result<User>.Success(
            new User(Normalise(email), passwordHash, googleSubject: null, displayName.Trim()));
    }

    public static Result<User> CreateFromGoogle(string email, string googleSubject, string displayName)
    {
        var validation = ValidateCore(email, displayName);
        if (validation.IsFailure) return Result<User>.Failure(validation.Error);

        return Result<User>.Success(
            new User(Normalise(email), passwordHash: null, googleSubject, displayName.Trim()));
    }

    public Result LinkGoogleAccount(string googleSubject)
    {
        if (GoogleSubject is not null && GoogleSubject != googleSubject)
            return Result.Failure(UserErrors.GoogleAlreadyLinked);

        GoogleSubject = googleSubject;
        return Result.Success();
    }

    public Result SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        SecurityStamp = Guid.CreateVersion7();
        return Result.Success();
    }

    public Result UpdateProfile(
        string displayName,
        string? bio,
        Sex? sex,
        ExperienceLevel? experienceLevel,
        UnitPreference units)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure(UserErrors.DisplayNameRequired);
        if (displayName.Trim().Length > MaxDisplayNameLength)
            return Result.Failure(UserErrors.DisplayNameTooLong);

        DisplayName = displayName.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        Sex = sex;
        ExperienceLevel = experienceLevel;
        Units = units;
        return Result.Success();
    }

    public Result<BodyweightEntry> RecordBodyweight(decimal weightKg, DateTimeOffset recordedAt)
    {
        if (weightKg < MinBodyweightKg || weightKg > MaxBodyweightKg)
            return Result<BodyweightEntry>.Failure(UserErrors.ImplausibleBodyweight);

        var entry = new BodyweightEntry(Id, weightKg, recordedAt);
        _bodyweightEntries.Add(entry);

        // Only the most recent reading defines "current". Backfilling history must not
        // rewrite the value that feeds DOTS and per-session bodyweight snapshots.
        if (CurrentBodyweightAt is null || recordedAt >= CurrentBodyweightAt)
        {
            CurrentBodyweightKg = weightKg;
            CurrentBodyweightAt = recordedAt;
        }

        return Result<BodyweightEntry>.Success(entry);
    }

    private static Result ValidateCore(string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure(UserErrors.EmailRequired);
        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure(UserErrors.DisplayNameRequired);
        if (displayName.Trim().Length > MaxDisplayNameLength)
            return Result.Failure(UserErrors.DisplayNameTooLong);

        return Result.Success();
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter UserTests`
Expected: PASS, 8 tests.

- [ ] **Step 7: Commit**

```bash
git add src/ProgressiveOverload.Domain/Users tests/ProgressiveOverload.Domain.Tests/Users
git commit -m "feat(domain): add User aggregate with profile and bodyweight history"
```

---

## Task 4: Refresh token entity with rotation and reuse detection

**Files:**
- Create: `src/ProgressiveOverload.Domain/Auth/{RefreshToken,AuthErrors}.cs`
- Test: `tests/ProgressiveOverload.Domain.Tests/Auth/RefreshTokenTests.cs`

**Interfaces:**
- Consumes: `Result`, `Error` (Task 2)
- Produces:
  - `RefreshToken.Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime, Guid? familyId = null) -> RefreshToken`
  - `token.Redeem(DateTimeOffset now) -> Result` — fails with `AuthErrors.RefreshTokenReused` or `RefreshTokenExpired`
  - `token.Revoke()`
  - Properties: `Id`, `UserId`, `TokenHash`, `FamilyId`, `ExpiresAt`, `RedeemedAt`, `RevokedAt`, `IsActive`

- [ ] **Step 1: Write the failing tests**

Create `tests/ProgressiveOverload.Domain.Tests/Auth/RefreshTokenTests.cs`:

```csharp
using ProgressiveOverload.Domain.Auth;
using Shouldly;

namespace ProgressiveOverload.Domain.Tests.Auth;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken AToken(Guid? familyId = null) =>
        RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now, Lifetime, familyId);

    [Fact]
    public void Issue_StartsANewFamilyWhenNoneGiven()
    {
        var token = AToken();
        token.FamilyId.ShouldNotBe(Guid.Empty);
        token.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Issue_InheritsTheFamilyWhenRotating()
    {
        var family = Guid.CreateVersion7();
        AToken(family).FamilyId.ShouldBe(family);
    }

    [Fact]
    public void Redeem_SucceedsOnceAndMarksTheToken()
    {
        var token = AToken();

        token.Redeem(Now).IsSuccess.ShouldBeTrue();
        token.RedeemedAt.ShouldBe(Now);
        token.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Redeem_SecondTimeIsReuse()
    {
        var token = AToken();
        token.Redeem(Now);

        var result = token.Redeem(Now.AddMinutes(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenReused);
    }

    [Fact]
    public void Redeem_AfterExpiryFails()
    {
        var token = AToken();
        var result = token.Redeem(Now + Lifetime + TimeSpan.FromSeconds(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.RefreshTokenExpired);
    }

    [Fact]
    public void Redeem_AfterRevocationFails()
    {
        var token = AToken();
        token.Revoke();

        token.Redeem(Now).IsFailure.ShouldBeTrue();
    }
}
```

The reuse test is the important one. A token presented twice means either a buggy client or a stolen token, and we cannot tell which — so we treat it as theft and revoke the whole family (wired up in Task 9).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter RefreshTokenTests`
Expected: FAIL — `RefreshToken` does not exist.

- [ ] **Step 3: Implement AuthErrors**

Create `src/ProgressiveOverload.Domain/Auth/AuthErrors.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email or password is incorrect.");

    public static readonly Error RefreshTokenInvalid =
        new("auth.refresh_token_invalid", "Your session has expired. Please sign in again.");

    public static readonly Error RefreshTokenExpired =
        new("auth.refresh_token_expired", "Your session has expired. Please sign in again.");

    public static readonly Error RefreshTokenReused =
        new("auth.refresh_token_reused", "Your session has expired. Please sign in again.");

    public static readonly Error GoogleTokenInvalid =
        new("auth.google_token_invalid", "Google sign-in failed. Please try again.");

    public static readonly Error GoogleEmailUnverified =
        new("auth.google_email_unverified", "Your Google email address is not verified.");
}
```

Note that `InvalidCredentials` is deliberately identical for "no such user" and "wrong password" — distinguishing them lets an attacker enumerate which emails are registered. Likewise all three refresh failures present the same message to the user while remaining distinct codes for our own logs.

- [ ] **Step 4: Implement RefreshToken**

Create `src/ProgressiveOverload.Domain/Auth/RefreshToken.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Domain.Auth;

public sealed class RefreshToken
{
    private RefreshToken() { } // EF Core

    private RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the opaque token. The raw token is never persisted.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>
    /// Shared by every token descended from one sign-in. Reuse of any token in the family
    /// revokes the entire family.
    /// </summary>
    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RedeemedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RedeemedAt is null && RevokedAt is null;

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        Guid? familyId = null) =>
        new(userId, tokenHash, familyId ?? Guid.CreateVersion7(), now + lifetime);

    public Result Redeem(DateTimeOffset now)
    {
        if (RedeemedAt is not null) return Result.Failure(AuthErrors.RefreshTokenReused);
        if (RevokedAt is not null) return Result.Failure(AuthErrors.RefreshTokenInvalid);
        if (now > ExpiresAt) return Result.Failure(AuthErrors.RefreshTokenExpired);

        RedeemedAt = now;
        return Result.Success();
    }

    public void Revoke()
    {
        RevokedAt ??= DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Domain.Tests --filter RefreshTokenTests`
Expected: PASS, 6 tests.

- [ ] **Step 6: Commit**

```bash
git add src/ProgressiveOverload.Domain/Auth tests/ProgressiveOverload.Domain.Tests/Auth
git commit -m "feat(domain): add refresh token with rotation and reuse detection"
```

---

## Task 5: Persistence — DbContext, configurations, initial migration

**Files:**
- Create: `src/ProgressiveOverload.Application/Persistence/AppDbContext.cs`
- Create: `src/ProgressiveOverload.Application/Persistence/Configurations/{UserConfiguration,BodyweightEntryConfiguration,RefreshTokenConfiguration}.cs`
- Create: `src/ProgressiveOverload.Application/Persistence/Migrations/` (EF-generated)
- Test: `tests/ProgressiveOverload.Integration.Tests/Infrastructure/PostgresFixture.cs`

**Interfaces:**
- Consumes: `User`, `BodyweightEntry`, `RefreshToken` (Tasks 3–4)
- Produces: `AppDbContext` with `DbSet<User> Users`, `DbSet<BodyweightEntry> BodyweightEntries`, `DbSet<RefreshToken> RefreshTokens`; a `PostgresFixture` giving integration tests a real database.

- [ ] **Step 1: Add packages**

```bash
dotnet add src/ProgressiveOverload.Application package Microsoft.EntityFrameworkCore
dotnet add src/ProgressiveOverload.Application package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/ProgressiveOverload.Application package EFCore.NamingConventions
dotnet add src/ProgressiveOverload.Application package Microsoft.EntityFrameworkCore.Design
dotnet add tests/ProgressiveOverload.Integration.Tests package Testcontainers.PostgreSql
dotnet add tests/ProgressiveOverload.Integration.Tests package Shouldly
dotnet add tests/ProgressiveOverload.Integration.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet tool install --global dotnet-ef
```

- [ ] **Step 2: Write the DbContext**

Create `src/ProgressiveOverload.Application/Persistence/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BodyweightEntry> BodyweightEntries => Set<BodyweightEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 3: Write the entity configurations**

Create `src/ProgressiveOverload.Application/Persistence/Configurations/UserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(256);

        builder.Property(u => u.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(u => u.GoogleSubject)
            .IsUnique()
            .HasFilter("google_subject IS NOT NULL");

        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);

        builder.Property(u => u.Sex).HasConversion<int>();
        builder.Property(u => u.ExperienceLevel).HasConversion<int>();
        builder.Property(u => u.Units).HasConversion<int>().IsRequired();

        builder.Property(u => u.CurrentBodyweightKg).HasPrecision(7, 2);

        builder.HasMany(u => u.BodyweightEntries)
            .WithOne()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.BodyweightEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

The filtered unique index on `google_subject` matters: a plain unique index would treat multiple NULLs as distinct in Postgres (which is what we want) but the filter makes the intent explicit and keeps the index small, since most users will not have linked Google.

Create `src/ProgressiveOverload.Application/Persistence/Configurations/BodyweightEntryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class BodyweightEntryConfiguration : IEntityTypeConfiguration<BodyweightEntry>
{
    public void Configure(EntityTypeBuilder<BodyweightEntry> builder)
    {
        builder.ToTable("bodyweight_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.WeightKg).HasPrecision(7, 2).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.RecordedAt }).IsDescending(false, true);
    }
}
```

Create `src/ProgressiveOverload.Application/Persistence/Configurations/RefreshTokenConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProgressiveOverload.Domain.Auth;

namespace ProgressiveOverload.Application.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);

        builder.Ignore(t => t.IsActive);
    }
}
```

- [ ] **Step 4: Configure snake_case naming and generate the migration**

Add to `AppDbContext.OnModelCreating`, before `base.OnModelCreating`:

```csharp
modelBuilder.UseSnakeCaseNamingConvention();
```

Then create the migration:

```bash
dotnet ef migrations add InitialSchema \
  --project src/ProgressiveOverload.Application \
  --startup-project src/ProgressiveOverload.Api \
  --output-dir Persistence/Migrations
```

If the tool cannot build a design-time context, add `src/ProgressiveOverload.Application/Persistence/DesignTimeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProgressiveOverload.Application.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=progressiveoverload;Username=po;Password=localdev")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
```

This connection string is for design-time migration generation against the local Docker container only. It is never used at runtime.

- [ ] **Step 5: Write the Testcontainers fixture**

Create `tests/ProgressiveOverload.Integration.Tests/Infrastructure/PostgresFixture.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Persistence;
using Testcontainers.PostgreSql;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
```

Note `MigrateAsync`, not `EnsureCreatedAsync`. Running the real migrations in tests means a broken migration fails CI rather than production.

- [ ] **Step 6: Write a test proving the schema applies**

Create `tests/ProgressiveOverload.Integration.Tests/Infrastructure/SchemaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

[Collection(nameof(PostgresCollection))]
public sealed class SchemaTests(PostgresFixture fixture)
{
    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task UserRoundTripsWithBodyweightHistory()
    {
        var user = User.CreateWithPassword($"{Guid.NewGuid():N}@example.com", "hash", "Jansen").Value;
        user.RecordBodyweight(84.5m, DateTimeOffset.UtcNow);

        await using (var db = NewContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var loaded = await db.Users
                .Include(u => u.BodyweightEntries)
                .SingleAsync(u => u.Id == user.Id);

            loaded.CurrentBodyweightKg.ShouldBe(84.5m);
            loaded.BodyweightEntries.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedByTheDatabase()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        await using var db = NewContext();
        db.Users.Add(User.CreateWithPassword(email, "hash", "One").Value);
        await db.SaveChangesAsync();

        db.Users.Add(User.CreateWithPassword(email, "hash", "Two").Value);
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
```

The second test is why we use a real Postgres rather than the EF in-memory provider — in-memory does not enforce unique indexes, so it would pass this test while production fails it.

- [ ] **Step 7: Run the integration tests**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests`
Expected: PASS, 2 tests. Docker must be running.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(infra): add DbContext, entity configurations, and initial migration"
```

---

## Task 6: Application ports and infrastructure adapters

**Files:**
- Create: `src/ProgressiveOverload.Application/Abstractions/{IPasswordHasher,ITokenService,ICurrentUser,IClock}.cs`
- Create: `src/ProgressiveOverload.Infrastructure/Auth/{PasswordHasherAdapter,JwtTokenService}.cs`, `src/ProgressiveOverload.Infrastructure/Time/SystemClock.cs`
- Create: `src/ProgressiveOverload.Infrastructure/DependencyInjection.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Auth/TokenServiceTests.cs`

**Interfaces:**
- Consumes: `User` (Task 3)
- Produces:
  - `IClock { DateTimeOffset UtcNow { get; } }`
  - `IPasswordHasher { string Hash(string password); bool Verify(string hash, string password); }`
  - `ITokenService { string CreateAccessToken(User user); (string Raw, string Hash) CreateRefreshToken(); string HashRefreshToken(string raw); }`
  - `ICurrentUser { Guid? UserId { get; } }`
  - `JwtOptions { Issuer, Audience, SigningKey, AccessTokenMinutes, RefreshTokenDays }`

- [ ] **Step 1: Add packages**

```bash
dotnet add src/ProgressiveOverload.Infrastructure package Microsoft.Extensions.Identity.Core
dotnet add src/ProgressiveOverload.Infrastructure package Microsoft.IdentityModel.JsonWebTokens
dotnet add src/ProgressiveOverload.Api package Microsoft.AspNetCore.Authentication.JwtBearer
```

`Microsoft.Extensions.Identity.Core` gives us `PasswordHasher<T>` — a well-reviewed PBKDF2 implementation with versioned formats — without dragging in the entire ASP.NET Identity stack, its `DbContext`, or its cookie pipeline.

- [ ] **Step 2: Write the ports**

Create `src/ProgressiveOverload.Application/Abstractions/IClock.cs`:

```csharp
namespace ProgressiveOverload.Application.Abstractions;

/// <summary>
/// Injected everywhere time is read. Calling DateTimeOffset.UtcNow directly in a handler
/// makes the behaviour untestable, and this codebase's most important logic (week
/// boundaries, token expiry) is time-dependent.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

Create `src/ProgressiveOverload.Application/Abstractions/IPasswordHasher.cs`:

```csharp
namespace ProgressiveOverload.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
```

Create `src/ProgressiveOverload.Application/Abstractions/ITokenService.cs`:

```csharp
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(User user);

    /// <summary>Returns the raw token to hand to the client, and the hash to persist.</summary>
    (string Raw, string Hash) CreateRefreshToken();

    string HashRefreshToken(string raw);
}
```

Create `src/ProgressiveOverload.Application/Abstractions/ICurrentUser.cs`:

```csharp
namespace ProgressiveOverload.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
```

- [ ] **Step 3: Write the adapters**

Create `src/ProgressiveOverload.Infrastructure/Time/SystemClock.cs`:

```csharp
using ProgressiveOverload.Application.Abstractions;

namespace ProgressiveOverload.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

Create `src/ProgressiveOverload.Infrastructure/Auth/PasswordHasherAdapter.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(null!, hash, password) is
            PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded;
}
```

Create `src/ProgressiveOverload.Application/Abstractions/JwtOptions.cs`. It lives in `Application`, not `Infrastructure`, because handlers read `RefreshTokenDays` from it — a configuration contract is not an infrastructure detail, and putting it in `Infrastructure` would invert the reference direction.

```csharp
namespace ProgressiveOverload.Application.Abstractions;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "progressiveoverload";
    public string Audience { get; init; } = "progressiveoverload";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}
```

Create `src/ProgressiveOverload.Infrastructure/Auth/JwtTokenService.cs`:

```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = clock.UtcNow.UtcDateTime,
            Expires = clock.UtcNow.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                ["stamp"] = user.SecurityStamp.ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
```

The refresh token is opaque random bytes, not a JWT — there is nothing to encode in it and a JWT would only invite someone to trust its contents. It is stored SHA-256 hashed, so a database leak does not yield usable sessions. Plain SHA-256 rather than a slow KDF is correct here: the token is 256 bits of CSPRNG output, so there is no dictionary to attack.

The `stamp` claim carries the user's `SecurityStamp`, which is what lets a password change invalidate outstanding access tokens without maintaining a blacklist.

Create `src/ProgressiveOverload.Infrastructure/Auth/CurrentUser.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using ProgressiveOverload.Application.Abstractions;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
```

Both claim types are checked because ASP.NET's default inbound claim mapping rewrites `sub` to `ClaimTypes.NameIdentifier` unless that mapping is disabled — reading only one of them produces an intermittently null user, which is a miserable bug to chase.

- [ ] **Step 4: Wire dependency injection**

Create `src/ProgressiveOverload.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Infrastructure.Auth;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Infrastructure.Time;

namespace ProgressiveOverload.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"))
                   .UseSnakeCaseNamingConvention());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
```

- [ ] **Step 5: Write tests for the token service**

Create `tests/ProgressiveOverload.Integration.Tests/Auth/TokenServiceTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Infrastructure.Auth;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

public sealed class TokenServiceTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private static JwtTokenService NewService() =>
        new(Options.Create(new JwtOptions
        {
            SigningKey = "a-test-signing-key-that-is-at-least-32-bytes-long",
            AccessTokenMinutes = 15
        }), new FixedClock());

    [Fact]
    public void AccessToken_CarriesSubjectAndSecurityStamp()
    {
        var user = User.CreateWithPassword("a@b.com", "hash", "Jansen").Value;
        var token = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(user));

        token.GetClaim(JwtRegisteredClaimNames.Sub).Value.ShouldBe(user.Id.ToString());
        token.GetClaim("stamp").Value.ShouldBe(user.SecurityStamp.ToString());
    }

    [Fact]
    public void AccessToken_ExpiresWithinTheConfiguredWindow()
    {
        var user = User.CreateWithPassword("a@b.com", "hash", "Jansen").Value;
        var token = new JsonWebTokenHandler().ReadJsonWebToken(NewService().CreateAccessToken(user));

        token.ValidTo.ShouldBe(new DateTime(2026, 7, 27, 12, 15, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RefreshTokens_AreUniqueAndHashDeterministically()
    {
        var service = NewService();
        var (rawA, hashA) = service.CreateRefreshToken();
        var (rawB, _) = service.CreateRefreshToken();

        rawA.ShouldNotBe(rawB);
        service.HashRefreshToken(rawA).ShouldBe(hashA);
        hashA.Length.ShouldBe(64);
    }
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter TokenServiceTests`
Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(infra): add clock, password hashing, and JWT token services"
```

---

## Task 7: Registration

**Files:**
- Create: `src/ProgressiveOverload.Application/Users/Register/{RegisterCommand,RegisterHandler,RegisterValidator}.cs`
- Create: `src/ProgressiveOverload.Api/Extensions/{ResultExtensions,ValidationExtensions}.cs`
- Create: `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/ProgressiveOverload.Api/Program.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Infrastructure/ApiFactory.cs`, `tests/ProgressiveOverload.Integration.Tests/Auth/RegisterTests.cs`

**Interfaces:**
- Consumes: `User`, `IPasswordHasher`, `AppDbContext`, `ITokenService`, `IClock`
- Produces:
  - `RegisterCommand(string Email, string Password, string DisplayName)`
  - `AuthResponse(string AccessToken, Guid UserId, string DisplayName)` — shared by register, login, refresh, and Google sign-in
  - `RegisterHandler.Handle(RegisterCommand, CancellationToken) -> Task<Result<AuthResult>>` where `AuthResult(AuthResponse Response, string RefreshTokenRaw)`
  - `POST /api/v1/auth/register`

- [ ] **Step 1: Add packages and the shared auth response**

```bash
dotnet add src/ProgressiveOverload.Application package FluentValidation
dotnet add src/ProgressiveOverload.Application package Microsoft.EntityFrameworkCore
dotnet add src/ProgressiveOverload.Api package FluentValidation.DependencyInjectionExtensions
```

`Application` referencing EF Core is intentional — handlers query `AppDbContext` directly. The abstraction we are protecting is the *domain*, which stays dependency-free; adding a repository layer here would buy nothing (spec §5).

Create `src/ProgressiveOverload.Application/Users/AuthResponse.cs`:

```csharp
namespace ProgressiveOverload.Application.Users;

public sealed record AuthResponse(string AccessToken, Guid UserId, string DisplayName);

/// <summary>
/// The raw refresh token is returned separately so the endpoint can place it in an
/// httpOnly cookie. It must never appear in a JSON response body (spec §7).
/// </summary>
public sealed record AuthResult(AuthResponse Response, string RefreshTokenRaw);
```

- [ ] **Step 2: Write the failing integration test**

Create `tests/ProgressiveOverload.Integration.Tests/Infrastructure/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ProgressiveOverload.Integration.Tests.Infrastructure;

public sealed class ApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fixture.ConnectionString,
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes",
                ["Jwt:Issuer"] = "progressiveoverload",
                ["Jwt:Audience"] = "progressiveoverload"
            }));

        return base.CreateHost(builder);
    }
}
```

Create `tests/ProgressiveOverload.Integration.Tests/Auth/RegisterTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RegisterTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static object ARegistration(string? email = null) => new
    {
        email = email ?? $"{Guid.NewGuid():N}@example.com",
        password = "correct horse battery staple",
        displayName = "Jansen"
    };

    [Fact]
    public async Task Register_ReturnsAccessTokenAndSetsRefreshCookie()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.DisplayName.ShouldBe("Jansen");

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.ShouldContain(c => c.StartsWith("po_refresh=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_NeverReturnsTheRefreshTokenInTheBody()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration());

        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain("refresh", Case.Insensitive);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration(email));
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", ARegistration(email));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "short",
            displayName = "Jansen"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter RegisterTests`
Expected: FAIL — 404, the endpoint does not exist.

- [ ] **Step 4: Write the command, validator, and handler**

Create `src/ProgressiveOverload.Application/Users/Register/RegisterCommand.cs`:

```csharp
namespace ProgressiveOverload.Application.Users.Register;

public sealed record RegisterCommand(string Email, string Password, string DisplayName);
```

Create `src/ProgressiveOverload.Application/Users/Register/RegisterValidator.cs`:

```csharp
using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public const int MinPasswordLength = 12;

    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);

        // NIST SP 800-63B: enforce length, drop composition rules. Requiring a symbol
        // and a digit produces "Password1!" and nothing else.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(256);

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
    }
}
```

Create `src/ProgressiveOverload.Application/Users/Register/RegisterHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.Register;

public sealed class RegisterHandler(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Result<AuthResult>.Failure(UserErrors.EmailAlreadyRegistered);

        var hash = passwordHasher.Hash(command.Password);
        var creation = User.CreateWithPassword(email, hash, command.DisplayName);
        if (creation.IsFailure)
            return Result<AuthResult>.Failure(creation.Error);

        var user = creation.Value;
        var (raw, tokenHash) = tokens.CreateRefreshToken();

        var refreshToken = RefreshToken.Issue(
            user.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays));

        db.Users.Add(user);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
```

The pre-check on email is for a clean error message, not for correctness — the unique index proven in Task 5 is what actually prevents duplicates under concurrency. Step 6 handles the race.

`RegisterHandler` needs `using Microsoft.Extensions.Options;` for `IOptions<JwtOptions>`. Both `JwtOptions` and `AppDbContext` live in `Application` (Tasks 5 and 6), so this compiles with no reference back into `Infrastructure`. Run `dotnet build` — if it reports a circular dependency, a type has been placed in the wrong project; check the File Structure section rather than adding a project reference.

- [ ] **Step 5: Write the Result → HTTP mapping**

Create `src/ProgressiveOverload.Api/Extensions/ResultExtensions.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Api.Extensions;

public static class ResultExtensions
{
    private static readonly Dictionary<string, int> StatusByErrorCode = new()
    {
        ["users.email_already_registered"] = StatusCodes.Status409Conflict,
        ["users.google_already_linked"] = StatusCodes.Status409Conflict,
        ["users.not_found"] = StatusCodes.Status404NotFound,
        ["auth.invalid_credentials"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_invalid"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_expired"] = StatusCodes.Status401Unauthorized,
        ["auth.refresh_token_reused"] = StatusCodes.Status401Unauthorized,
        ["auth.google_token_invalid"] = StatusCodes.Status401Unauthorized,
        ["auth.google_email_unverified"] = StatusCodes.Status403Forbidden
    };

    public static IResult ToProblem(this Error error)
    {
        var status = StatusByErrorCode.TryGetValue(error.Code, out var mapped)
            ? mapped
            : StatusCodes.Status400BadRequest;

        return Results.Problem(
            title: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
```

Every error reaches the client as RFC 7807 `ProblemDetails` with a stable machine-readable `code`, so the frontend branches on codes rather than parsing prose (spec §8).

- [ ] **Step 6: Write the endpoint and wire Program.cs**

Create `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Api.Extensions;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookieName = "po_refresh";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterCommand command,
            IValidator<RegisterCommand> validator,
            RegisterHandler handler,
            IOptions<JwtOptions> jwtOptions,
            HttpContext http,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            Result<AuthResult> result;
            try
            {
                result = await handler.Handle(command, ct);
            }
            catch (DbUpdateException)
            {
                // Lost the race on the unique email index.
                return UserErrors.EmailAlreadyRegistered.ToProblem();
            }

            if (result.IsFailure) return result.Error.ToProblem();

            http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
            return Results.Created($"/api/v1/users/{result.Value.Response.UserId}", result.Value.Response);
        })
        .AllowAnonymous();
    }

    public static void SetRefreshCookie(this HttpContext http, string raw, int days) =>
        http.Response.Cookies.Append(RefreshCookieName, raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            IsEssential = true
        });
}
```

Tasks 8–10 each add one endpoint to this file and must add the matching `using` for their own feature namespace (`...Users.Login`, `...Users.Refresh`, `...Users.Logout`, `...Users.GoogleSignIn`) and `ProgressiveOverload.Domain.Auth` for `AuthErrors`. Because `EnforceCodeStyleInBuild` is on, adding them before they are used will fail the build — add each one in the task that needs it.

`SameSite=Strict` and a `Path` scoped to `/api/v1/auth` are both deliberate. Strict is viable precisely because the spec puts the API on `api.progressiveoverload.app`, same-site with the web app; the narrow path means the cookie is not attached to every unrelated API call.

Replace `src/ProgressiveOverload.Api/Program.cs`:

```csharp
using FluentValidation;
using ProgressiveOverload.Api.Endpoints;
using ProgressiveOverload.Application.Users.Register;
using ProgressiveOverload.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapAuthEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

The `public partial class Program` declaration at the end is required for `WebApplicationFactory<Program>` in the integration tests to find the entry point.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter RegisterTests`
Expected: PASS, 4 tests.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(auth): add registration endpoint with validation and refresh cookie"
```

---

## Task 8: Login

**Files:**
- Create: `src/ProgressiveOverload.Application/Users/Login/{LoginCommand,LoginHandler,LoginValidator}.cs`
- Modify: `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`, `src/ProgressiveOverload.Api/Program.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Auth/LoginTests.cs`

**Interfaces:**
- Consumes: `AuthResult`, `AuthResponse`, `IPasswordHasher`, `ITokenService`, `IClock`, `AppDbContext`
- Produces: `LoginCommand(string Email, string Password)`; `LoginHandler.Handle(LoginCommand, CancellationToken) -> Task<Result<AuthResult>>`; `POST /api/v1/auth/login`

- [ ] **Step 1: Write the failing tests**

Create `tests/ProgressiveOverload.Integration.Tests/Auth/LoginTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class LoginTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);
    private const string Password = "correct horse battery staple";

    public void Dispose() => _factory.Dispose();

    private async Task<(HttpClient Client, string Email)> ARegisteredUser()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = Password, displayName = "Jansen" });

        return (client, email);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = email.ToUpperInvariant(), password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var (client, email) = await ARegisteredUser();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "not the right password at all" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_AreIndistinguishable()
    {
        var (client, email) = await ARegisteredUser();

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"{Guid.NewGuid():N}@example.com", password = Password });
        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "definitely wrong password" });

        unknown.StatusCode.ShouldBe(wrong.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .ShouldBe(await wrong.Content.ReadAsStringAsync());
    }
}
```

That last test is a real security requirement, not a nicety: differing responses turn the login endpoint into an account-enumeration oracle.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter LoginTests`
Expected: FAIL — 404 on `/api/v1/auth/login`.

- [ ] **Step 3: Write the command, validator, and handler**

Create `src/ProgressiveOverload.Application/Users/Login/LoginCommand.cs`:

```csharp
namespace ProgressiveOverload.Application.Users.Login;

public sealed record LoginCommand(string Email, string Password);
```

Create `src/ProgressiveOverload.Application/Users/Login/LoginValidator.cs`:

```csharp
using FluentValidation;

namespace ProgressiveOverload.Application.Users.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
```

Deliberately no minimum length here. Validating password length on login would reject an existing user whose password predates a policy change, and it leaks the policy to an attacker.

Create `src/ProgressiveOverload.Application/Users/Login/LoginHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.Login;

public sealed class LoginHandler(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(LoginCommand command, CancellationToken ct)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        // Hash even when the user is absent, so response time does not reveal
        // whether the email is registered.
        var hashToVerify = user?.PasswordHash ?? DummyHash.Value;
        var passwordValid = passwordHasher.Verify(hashToVerify, command.Password);

        if (user is null || user.PasswordHash is null || !passwordValid)
            return Result<AuthResult>.Failure(AuthErrors.InvalidCredentials);

        var (raw, tokenHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}

internal static class DummyHash
{
    /// <summary>
    /// A real PBKDF2 hash of a random string, computed once. Verifying against it costs
    /// the same as verifying a genuine user, which closes the timing side channel that
    /// would otherwise reveal whether an email exists.
    /// </summary>
    public static readonly string Value =
        new Microsoft.AspNetCore.Identity.PasswordHasher<object>()
            .HashPassword(new object(), Guid.NewGuid().ToString());
}
```

`user.PasswordHash is null` covers the Google-only account: someone who signed up with Google has no password, and must not be able to authenticate by supplying one.

- [ ] **Step 4: Map the endpoint**

Add inside `MapAuthEndpoints` in `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`:

```csharp
group.MapPost("/login", async (
    LoginCommand command,
    IValidator<LoginCommand> validator,
    LoginHandler handler,
    IOptions<JwtOptions> jwtOptions,
    HttpContext http,
    CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(command, ct);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await handler.Handle(command, ct);
    if (result.IsFailure) return result.Error.ToProblem();

    http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
    return Results.Ok(result.Value.Response);
})
.AllowAnonymous();
```

Register the handler in `Program.cs`:

```csharp
builder.Services.AddScoped<LoginHandler>();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter LoginTests`
Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(auth): add login with constant-time credential failure"
```

---

## Task 9: Refresh rotation, reuse detection, and logout

**Files:**
- Create: `src/ProgressiveOverload.Application/Users/Refresh/{RefreshCommand,RefreshHandler}.cs`
- Create: `src/ProgressiveOverload.Application/Users/Logout/{LogoutCommand,LogoutHandler}.cs`
- Modify: `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`, `src/ProgressiveOverload.Api/Program.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Auth/RefreshRotationTests.cs`

**Interfaces:**
- Consumes: `RefreshToken`, `AuthResult`, `ITokenService`, `IClock`, `AppDbContext`
- Produces: `RefreshHandler.Handle(string rawToken, CancellationToken) -> Task<Result<AuthResult>>`; `LogoutHandler.Handle(string? rawToken, CancellationToken) -> Task<Result>`; `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`

- [ ] **Step 1: Write the failing tests**

Create `tests/ProgressiveOverload.Integration.Tests/Auth/RefreshRotationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Persistence;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RefreshRotationTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private static string ExtractRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("po_refresh="))
            .Split(';')[0]["po_refresh=".Length..];

    private async Task<(HttpClient Client, string Refresh)> ASignedInUser()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });

        return (client, ExtractRefreshCookie(response));
    }

    private static HttpRequestMessage RefreshRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"po_refresh={token}");
        return request;
    }

    [Fact]
    public async Task Refresh_IssuesANewTokenPair()
    {
        var (client, refresh) = await ASignedInUser();

        var response = await client.SendAsync(RefreshRequest(refresh));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExtractRefreshCookie(response).ShouldNotBe(refresh);
    }

    [Fact]
    public async Task Refresh_WithNoCookie_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/auth/refresh", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReusingAnOldToken_RevokesTheEntireFamily()
    {
        var (client, original) = await ASignedInUser();

        var rotated = await client.SendAsync(RefreshRequest(original));
        var newToken = ExtractRefreshCookie(rotated);

        // Replay the already-redeemed token: this is the theft signal.
        var replay = await client.SendAsync(RefreshRequest(original));
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The token the legitimate client holds must now also be dead.
        var afterBreach = await client.SendAsync(RefreshRequest(newToken));
        afterBreach.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheFamilyAndClearsTheCookie()
    {
        var (client, refresh) = await ASignedInUser();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"po_refresh={refresh}");
        var logout = await client.SendAsync(request);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await client.SendAsync(RefreshRequest(refresh));
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

`ReusingAnOldToken_RevokesTheEntireFamily` is the most valuable test in this milestone. It asserts that a stolen token cannot be used indefinitely alongside the real user's session: the moment either party replays a redeemed token, both are logged out and the theft becomes visible.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter RefreshRotationTests`
Expected: FAIL — 404 on `/api/v1/auth/refresh`.

- [ ] **Step 3: Write the refresh handler**

Create `src/ProgressiveOverload.Application/Users/Refresh/RefreshHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.Refresh;

public sealed class RefreshHandler(
    AppDbContext db,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(string rawToken, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenInvalid);

        var redemption = stored.Redeem(clock.UtcNow);
        if (redemption.IsFailure)
        {
            // Replaying a redeemed token means the token was captured — we cannot tell
            // whether we are talking to the thief or the victim, so end every session
            // descended from this sign-in and force a fresh login.
            if (redemption.Error == AuthErrors.RefreshTokenReused)
            {
                await db.RefreshTokens
                    .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);
            }

            return Result<AuthResult>.Failure(redemption.Error);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null)
            return Result<AuthResult>.Failure(AuthErrors.RefreshTokenInvalid);

        var (raw, newHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, newHash, clock.UtcNow,
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays), stored.FamilyId));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
```

- [ ] **Step 4: Write the logout handler**

Create `src/ProgressiveOverload.Application/Users/Logout/LogoutHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.Logout;

public sealed class LogoutHandler(AppDbContext db, ITokenService tokens, IClock clock)
{
    public async Task<Result> Handle(string? rawToken, CancellationToken ct)
    {
        // Logout is always reported as successful. Telling an unauthenticated caller
        // whether a token was valid gives away information for no benefit.
        if (string.IsNullOrWhiteSpace(rawToken)) return Result.Success();

        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return Result.Success();

        await db.RefreshTokens
            .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), ct);

        return Result.Success();
    }
}
```

- [ ] **Step 5: Map the endpoints**

Add inside `MapAuthEndpoints`:

```csharp
group.MapPost("/refresh", async (
    RefreshHandler handler,
    IOptions<JwtOptions> jwtOptions,
    HttpContext http,
    CancellationToken ct) =>
{
    var raw = http.Request.Cookies[RefreshCookieName];
    if (string.IsNullOrWhiteSpace(raw))
        return AuthErrors.RefreshTokenInvalid.ToProblem();

    var result = await handler.Handle(raw, ct);
    if (result.IsFailure)
    {
        http.ClearRefreshCookie();
        return result.Error.ToProblem();
    }

    http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
    return Results.Ok(result.Value.Response);
})
.AllowAnonymous();

group.MapPost("/logout", async (LogoutHandler handler, HttpContext http, CancellationToken ct) =>
{
    await handler.Handle(http.Request.Cookies[RefreshCookieName], ct);
    http.ClearRefreshCookie();
    return Results.NoContent();
})
.AllowAnonymous();
```

Add to `AuthEndpoints`:

```csharp
public static void ClearRefreshCookie(this HttpContext http) =>
    http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth"
    });
```

The delete options must match the set options exactly, or the browser keeps the original cookie and the user appears stuck in a broken half-session.

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<LogoutHandler>();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter RefreshRotationTests`
Expected: PASS, 4 tests.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(auth): rotate refresh tokens with family revocation on reuse"
```

---

## Task 10: Google sign-in

**Files:**
- Create: `src/ProgressiveOverload.Application/Users/GoogleSignIn/{GoogleSignInCommand,GoogleSignInHandler}.cs`
- Create: `src/ProgressiveOverload.Application/Abstractions/IGoogleTokenValidator.cs`
- Create: `src/ProgressiveOverload.Infrastructure/Auth/GoogleTokenValidator.cs`
- Modify: `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`, `Program.cs`, `DependencyInjection.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Auth/GoogleSignInTests.cs`

**Interfaces:**
- Consumes: `User`, `AuthResult`, `ITokenService`, `IClock`, `AppDbContext`
- Produces:
  - `GooglePayload(string Subject, string Email, bool EmailVerified, string? Name)`
  - `IGoogleTokenValidator { Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct); }`
  - `GoogleSignInHandler.Handle(GoogleSignInCommand, CancellationToken) -> Task<Result<AuthResult>>`
  - `POST /api/v1/auth/google`

- [ ] **Step 1: Add the package**

```bash
dotnet add src/ProgressiveOverload.Infrastructure package Google.Apis.Auth
```

- [ ] **Step 2: Write the port**

Create `src/ProgressiveOverload.Application/Abstractions/IGoogleTokenValidator.cs`:

```csharp
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Application.Abstractions;

public sealed record GooglePayload(string Subject, string Email, bool EmailVerified, string? Name);

public interface IGoogleTokenValidator
{
    Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct);
}
```

The port exists so the handler's linking logic can be tested with a fake, without network calls to Google.

- [ ] **Step 3: Write the failing tests**

Create `tests/ProgressiveOverload.Integration.Tests/Auth/GoogleSignInTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class GoogleSignInTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private sealed class FakeGoogleValidator : IGoogleTokenValidator
    {
        public GooglePayload? Payload { get; set; }

        public Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct) =>
            Task.FromResult(Payload is null
                ? Result<GooglePayload>.Failure(AuthErrors.GoogleTokenInvalid)
                : Result<GooglePayload>.Success(Payload));
    }

    private HttpClient ClientWith(GooglePayload? payload)
    {
        var fake = new FakeGoogleValidator { Payload = payload };
        return _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IGoogleTokenValidator>(fake))).CreateClient();
    }

    [Fact]
    public async Task GoogleSignIn_CreatesAccountForNewEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = ClientWith(new GooglePayload("sub-1", email, EmailVerified: true, "Jansen"));

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GoogleSignIn_RejectsUnverifiedEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var client = ClientWith(new GooglePayload("sub-2", email, EmailVerified: false, "Jansen"));

        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GoogleSignIn_LinksToExistingPasswordAccountWithSameEmail()
    {
        var email = $"{Guid.NewGuid():N}@example.com";

        var passwordClient = _factory.CreateClient();
        var registered = await passwordClient.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "correct horse battery staple", displayName = "Jansen" });
        var originalUserId = (await registered.Content
            .ReadFromJsonAsync<ProgressiveOverload.Application.Users.AuthResponse>())!.UserId;

        var client = ClientWith(new GooglePayload("sub-3", email, EmailVerified: true, "Jansen"));
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<ProgressiveOverload.Application.Users.AuthResponse>();
        body!.UserId.ShouldBe(originalUserId);
    }

    [Fact]
    public async Task GoogleSignIn_RejectsInvalidToken()
    {
        var client = ClientWith(null);
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "garbage" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

`RejectsUnverifiedEmail` is the account-takeover guard from spec §7. Without it, anyone able to obtain a Google token for an unverified address matching an existing user's email takes over that account.

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter GoogleSignInTests`
Expected: FAIL — 404 on `/api/v1/auth/google`.

- [ ] **Step 5: Write the validator adapter**

Create `src/ProgressiveOverload.Infrastructure/Auth/GoogleTokenValidator.cs`:

```csharp
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;

namespace ProgressiveOverload.Infrastructure.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    public string ClientId { get; init; } = string.Empty;
}

public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    public async Task<Result<GooglePayload>> Validate(string idToken, CancellationToken ct)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    // Pinning the audience is what stops a token minted for a different
                    // application from being replayed against ours.
                    Audience = [options.Value.ClientId]
                });

            return Result<GooglePayload>.Success(
                new GooglePayload(payload.Subject, payload.Email, payload.EmailVerified, payload.Name));
        }
        catch (InvalidJwtException)
        {
            return Result<GooglePayload>.Failure(AuthErrors.GoogleTokenInvalid);
        }
    }
}
```

- [ ] **Step 6: Write the handler**

Create `src/ProgressiveOverload.Application/Users/GoogleSignIn/GoogleSignInCommand.cs`:

```csharp
namespace ProgressiveOverload.Application.Users.GoogleSignIn;

public sealed record GoogleSignInCommand(string IdToken);
```

Create `src/ProgressiveOverload.Application/Users/GoogleSignIn/GoogleSignInHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Auth;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.GoogleSignIn;

public sealed class GoogleSignInHandler(
    AppDbContext db,
    IGoogleTokenValidator google,
    ITokenService tokens,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<AuthResult>> Handle(GoogleSignInCommand command, CancellationToken ct)
    {
        var validation = await google.Validate(command.IdToken, ct);
        if (validation.IsFailure)
            return Result<AuthResult>.Failure(validation.Error);

        var payload = validation.Value;

        // Linking an identity to an existing account on the strength of an unverified
        // email address is a straightforward takeover. Google sets this claim; trust it
        // only when it is true.
        if (!payload.EmailVerified)
            return Result<AuthResult>.Failure(AuthErrors.GoogleEmailUnverified);

        var email = payload.Email.Trim().ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == payload.Subject, ct)
                   ?? await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            var creation = User.CreateFromGoogle(email, payload.Subject, payload.Name ?? email.Split('@')[0]);
            if (creation.IsFailure) return Result<AuthResult>.Failure(creation.Error);

            user = creation.Value;
            db.Users.Add(user);
        }
        else
        {
            var link = user.LinkGoogleAccount(payload.Subject);
            if (link.IsFailure) return Result<AuthResult>.Failure(link.Error);
        }

        var (raw, tokenHash) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(RefreshToken.Issue(
            user.Id, tokenHash, clock.UtcNow, TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)));

        await db.SaveChangesAsync(ct);

        return Result<AuthResult>.Success(new AuthResult(
            new AuthResponse(tokens.CreateAccessToken(user), user.Id, user.DisplayName),
            raw));
    }
}
```

Lookup is by Google subject first, email second. The subject is Google's stable identifier and never changes; email addresses can be changed by the user, so matching on email alone would eventually attach one person's sign-in to another person's account.

- [ ] **Step 7: Map the endpoint and register services**

Add inside `MapAuthEndpoints`:

```csharp
group.MapPost("/google", async (
    GoogleSignInCommand command,
    GoogleSignInHandler handler,
    IOptions<JwtOptions> jwtOptions,
    HttpContext http,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(command.IdToken))
        return AuthErrors.GoogleTokenInvalid.ToProblem();

    var result = await handler.Handle(command, ct);
    if (result.IsFailure) return result.Error.ToProblem();

    http.SetRefreshCookie(result.Value.RefreshTokenRaw, jwtOptions.Value.RefreshTokenDays);
    return Results.Ok(result.Value.Response);
})
.AllowAnonymous();
```

In `DependencyInjection.AddInfrastructure`:

```csharp
services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
```

In `Program.cs`:

```csharp
builder.Services.AddScoped<GoogleSignInHandler>();
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter GoogleSignInTests`
Expected: PASS, 4 tests.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(auth): add Google sign-in with verified-email linking"
```

---

## Task 11: Profile and bodyweight endpoints

**Files:**
- Create: `src/ProgressiveOverload.Application/Users/GetProfile/{GetProfileHandler,ProfileResponse}.cs`
- Create: `src/ProgressiveOverload.Application/Users/UpdateProfile/{UpdateProfileCommand,UpdateProfileHandler,UpdateProfileValidator}.cs`
- Create: `src/ProgressiveOverload.Application/Users/RecordBodyweight/{RecordBodyweightCommand,RecordBodyweightHandler,RecordBodyweightValidator}.cs`
- Create: `src/ProgressiveOverload.Api/Endpoints/ProfileEndpoints.cs`
- Modify: `src/ProgressiveOverload.Api/Program.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Profile/ProfileTests.cs`

**Interfaces:**
- Consumes: `User`, `ICurrentUser`, `IClock`, `AppDbContext`
- Produces:
  - `ProfileResponse(Guid Id, string Email, string DisplayName, string? Bio, string? AvatarUrl, Sex? Sex, ExperienceLevel? ExperienceLevel, UnitPreference Units, decimal? CurrentBodyweightKg, DateTimeOffset CreatedAt)`
  - `UpdateProfileCommand(string DisplayName, string? Bio, Sex? Sex, ExperienceLevel? ExperienceLevel, UnitPreference Units)`
  - `RecordBodyweightCommand(decimal WeightKg, DateTimeOffset? RecordedAt)`
  - `GET /api/v1/me`, `PATCH /api/v1/me`, `POST /api/v1/me/bodyweight`

- [ ] **Step 1: Add JWT bearer authentication to Program.cs**

Add these usings at the top of `Program.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProgressiveOverload.Application.Abstractions;
```

Insert before `builder.Build()`:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

        options.MapInboundClaims = false; // keep "sub" as "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
```

And after `var app = builder.Build();`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

`MapInboundClaims = false` stops ASP.NET rewriting `sub` into `ClaimTypes.NameIdentifier`. The default `ClockSkew` is five minutes, which would keep a 15-minute access token alive for twenty; 30 seconds is enough for real clock drift.

- [ ] **Step 2: Write the failing tests**

Create `tests/ProgressiveOverload.Integration.Tests/Profile/ProfileTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProgressiveOverload.Application.Users;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Profile;

[Collection(nameof(PostgresCollection))]
public sealed class ProfileTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> AnAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Jansen"
        });

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_ReturnsTheAuthenticatedUser()
    {
        var client = await AnAuthenticatedClient();

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");

        profile.ShouldNotBeNull();
        profile.DisplayName.ShouldBe("Jansen");
        profile.CurrentBodyweightKg.ShouldBeNull();
    }

    [Fact]
    public async Task PatchMe_UpdatesProfileFields()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PatchAsJsonAsync("/api/v1/me", new
        {
            displayName = "Jansen A",
            bio = "Chasing a 200kg squat.",
            sex = 1,
            experienceLevel = 3,
            units = 2
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        profile!.DisplayName.ShouldBe("Jansen A");
        profile.Bio.ShouldBe("Chasing a 200kg squat.");
    }

    [Fact]
    public async Task PatchMe_RejectsOverlongDisplayName()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PatchAsJsonAsync("/api/v1/me", new
        {
            displayName = new string('x', 31),
            bio = (string?)null,
            sex = (int?)null,
            experienceLevel = (int?)null,
            units = 1
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBodyweight_UpdatesCurrentWeight()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/bodyweight", new { weightKg = 84.5m });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        profile!.CurrentBodyweightKg.ShouldBe(84.5m);
    }

    [Fact]
    public async Task PostBodyweight_RejectsImplausibleValue()
    {
        var client = await AnAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/me/bodyweight", new { weightKg = 900m });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OneUsersTokenNeverReturnsAnotherUsersProfile()
    {
        var alice = await AnAuthenticatedClient();
        var bob = await AnAuthenticatedClient();

        var aliceProfile = await alice.GetFromJsonAsync<ProfileResponse>("/api/v1/me");
        var bobProfile = await bob.GetFromJsonAsync<ProfileResponse>("/api/v1/me");

        aliceProfile!.Id.ShouldNotBe(bobProfile!.Id);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter ProfileTests`
Expected: FAIL — 404 on `/api/v1/me`.

- [ ] **Step 4: Write the response DTO and handlers**

Create `src/ProgressiveOverload.Application/Users/GetProfile/ProfileResponse.cs`:

```csharp
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.GetProfile;

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    Sex? Sex,
    ExperienceLevel? ExperienceLevel,
    UnitPreference Units,
    decimal? CurrentBodyweightKg,
    DateTimeOffset CreatedAt);
```

Create `src/ProgressiveOverload.Application/Users/GetProfile/GetProfileHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.GetProfile;

public sealed class GetProfileHandler(AppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProfileResponse>> Handle(CancellationToken ct)
    {
        // The identity always comes from the authenticated principal. No endpoint in
        // this codebase accepts a user id from the caller (spec §7).
        var userId = currentUser.UserId;
        if (userId is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var profile = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new ProfileResponse(
                u.Id, u.Email, u.DisplayName, u.Bio, u.AvatarUrl,
                u.Sex, u.ExperienceLevel, u.Units, u.CurrentBodyweightKg, u.CreatedAt))
            .SingleOrDefaultAsync(ct);

        return profile is null
            ? Result<ProfileResponse>.Failure(UserErrors.NotFound)
            : Result<ProfileResponse>.Success(profile);
    }
}
```

Create `src/ProgressiveOverload.Application/Users/UpdateProfile/UpdateProfileCommand.cs`:

```csharp
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed record UpdateProfileCommand(
    string DisplayName,
    string? Bio,
    Sex? Sex,
    ExperienceLevel? ExperienceLevel,
    UnitPreference Units);
```

Create `src/ProgressiveOverload.Application/Users/UpdateProfile/UpdateProfileValidator.cs`:

```csharp
using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(User.MaxDisplayNameLength);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x.Sex).IsInEnum().When(x => x.Sex.HasValue);
        RuleFor(x => x.ExperienceLevel).IsInEnum().When(x => x.ExperienceLevel.HasValue);
        RuleFor(x => x.Units).IsInEnum();
    }
}
```

Create `src/ProgressiveOverload.Application/Users/UpdateProfile/UpdateProfileHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.UpdateProfile;

public sealed class UpdateProfileHandler(AppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ProfileResponse>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null) return Result<ProfileResponse>.Failure(UserErrors.NotFound);

        var update = user.UpdateProfile(
            command.DisplayName, command.Bio, command.Sex, command.ExperienceLevel, command.Units);

        if (update.IsFailure) return Result<ProfileResponse>.Failure(update.Error);

        await db.SaveChangesAsync(ct);

        return Result<ProfileResponse>.Success(new ProfileResponse(
            user.Id, user.Email, user.DisplayName, user.Bio, user.AvatarUrl,
            user.Sex, user.ExperienceLevel, user.Units, user.CurrentBodyweightKg, user.CreatedAt));
    }
}
```

Create `src/ProgressiveOverload.Application/Users/RecordBodyweight/RecordBodyweightCommand.cs`:

```csharp
namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed record RecordBodyweightCommand(decimal WeightKg, DateTimeOffset? RecordedAt);

public sealed record BodyweightResponse(Guid Id, decimal WeightKg, DateTimeOffset RecordedAt);
```

Create `src/ProgressiveOverload.Application/Users/RecordBodyweight/RecordBodyweightValidator.cs`:

```csharp
using FluentValidation;
using ProgressiveOverload.Domain.Users;

namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed class RecordBodyweightValidator : AbstractValidator<RecordBodyweightCommand>
{
    public RecordBodyweightValidator()
    {
        RuleFor(x => x.WeightKg)
            .InclusiveBetween(User.MinBodyweightKg, User.MaxBodyweightKg);
    }
}
```

Create `src/ProgressiveOverload.Application/Users/RecordBodyweight/RecordBodyweightHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ProgressiveOverload.Application.Abstractions;
using ProgressiveOverload.Domain.Common;
using ProgressiveOverload.Domain.Users;
using ProgressiveOverload.Application.Persistence;

namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed class RecordBodyweightHandler(AppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<Result<BodyweightResponse>> Handle(RecordBodyweightCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return Result<BodyweightResponse>.Failure(UserErrors.NotFound);

        var user = await db.Users
            .Include(u => u.BodyweightEntries)
            .SingleOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user is null) return Result<BodyweightResponse>.Failure(UserErrors.NotFound);

        var recorded = user.RecordBodyweight(command.WeightKg, command.RecordedAt ?? clock.UtcNow);
        if (recorded.IsFailure) return Result<BodyweightResponse>.Failure(recorded.Error);

        await db.SaveChangesAsync(ct);

        return Result<BodyweightResponse>.Success(new BodyweightResponse(
            recorded.Value.Id, recorded.Value.WeightKg, recorded.Value.RecordedAt));
    }
}
```

- [ ] **Step 5: Write the endpoints**

Create `src/ProgressiveOverload.Api/Endpoints/ProfileEndpoints.cs`:

```csharp
using FluentValidation;
using ProgressiveOverload.Api.Extensions;
using ProgressiveOverload.Application.Users.GetProfile;
using ProgressiveOverload.Application.Users.RecordBodyweight;
using ProgressiveOverload.Application.Users.UpdateProfile;

namespace ProgressiveOverload.Api.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/me").WithTags("Profile").RequireAuthorization();

        group.MapGet("/", async (GetProfileHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPatch("/", async (
            UpdateProfileCommand command,
            IValidator<UpdateProfileCommand> validator,
            UpdateProfileHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPost("/bodyweight", async (
            RecordBodyweightCommand command,
            IValidator<RecordBodyweightCommand> validator,
            RecordBodyweightHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.Handle(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/me/bodyweight/{result.Value.Id}", result.Value)
                : result.Error.ToProblem();
        });
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<GetProfileHandler>();
builder.Services.AddScoped<UpdateProfileHandler>();
builder.Services.AddScoped<RecordBodyweightHandler>();
// ...
app.MapProfileEndpoints();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter ProfileTests`
Expected: PASS, 7 tests.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(profile): add profile read/update and bodyweight tracking"
```

---

## Task 12: Cross-cutting — rate limiting, Serilog, Sentry

**Files:**
- Modify: `src/ProgressiveOverload.Api/Program.cs`, `src/ProgressiveOverload.Api/appsettings.json`
- Modify: `src/ProgressiveOverload.Api/Endpoints/AuthEndpoints.cs`
- Test: `tests/ProgressiveOverload.Integration.Tests/Auth/RateLimitTests.cs`

**Interfaces:**
- Consumes: everything above
- Produces: a `"strict-auth"` rate limit policy applied to `/auth/login`, `/auth/register`, and `/auth/google`; Serilog request logging; Sentry with PII scrubbing

- [ ] **Step 1: Add packages**

```bash
dotnet add src/ProgressiveOverload.Api package Serilog.AspNetCore
dotnet add src/ProgressiveOverload.Api package Sentry.AspNetCore
```

- [ ] **Step 2: Write the failing rate limit test**

Create `tests/ProgressiveOverload.Integration.Tests/Auth/RateLimitTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using ProgressiveOverload.Integration.Tests.Infrastructure;
using Shouldly;

namespace ProgressiveOverload.Integration.Tests.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RateLimitTests(PostgresFixture fixture) : IDisposable
{
    private readonly ApiFactory _factory = new(fixture);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RepeatedFailedLogins_AreRateLimited()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "wrong password attempt" });
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/ProgressiveOverload.Integration.Tests --filter RateLimitTests`
Expected: FAIL — all 12 responses are 401, none are 429.

- [ ] **Step 4: Add rate limiting to Program.cs**

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Partitioned per IP. Correct for a single Render instance (spec §10); revisit if
    // the API is ever scaled out, since in-memory partitions are per-process.
    options.AddPolicy("strict-auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
```

After `var app = builder.Build();`, before `UseAuthentication`:

```csharp
app.UseRateLimiter();
```

Then apply the policy in `AuthEndpoints` — add `.RequireRateLimiting("strict-auth")` to the `/register`, `/login`, and `/google` endpoint definitions.

- [ ] **Step 5: Add Serilog and Sentry**

At the very top of `Program.cs`:

```csharp
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? string.Empty;
    options.Environment = builder.Environment.EnvironmentName;
    options.Release = builder.Configuration["Sentry:Release"];
    options.TracesSampleRate = 0.1;

    // The app handles email addresses and bodyweight — health-adjacent personal data
    // that must never reach a third-party error tracker (spec §11).
    options.SendDefaultPii = false;
    options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
    options.SetBeforeSend((@event, _) =>
        builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? null
            : @event);
});
```

After `var app = builder.Build();`:

```csharp
app.UseSerilogRequestLogging();
```

Update `src/ProgressiveOverload.Api/appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "Jwt": {
    "Issuer": "progressiveoverload",
    "Audience": "progressiveoverload",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30
  },
  "AllowedHosts": "*"
}
```

`Jwt:SigningKey`, `ConnectionStrings:Postgres`, `GoogleAuth:ClientId`, and `Sentry:Dsn` are deliberately absent — they are secrets, supplied by `dotnet user-secrets` locally and environment variables in production. Never commit them.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS, all tests across both projects.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(api): add rate limiting, structured logging, and Sentry"
```

---

## Task 13: CI pipeline

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: the whole solution
- Produces: a CI run that builds, checks formatting, and runs all tests on every PR

- [ ] **Step 1: Write the workflow**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Check formatting
        run: dotnet format --verify-no-changes --no-restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      # Testcontainers uses the Docker daemon that is preinstalled on ubuntu-latest.
      - name: Test
        run: dotnet test --no-build --configuration Release --logger "trx;LogFileName=results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/results.trx'
```

- [ ] **Step 2: Verify formatting passes locally before pushing**

Run: `dotnet format --verify-no-changes`
Expected: exit code 0. If it fails, run `dotnet format` and commit the result.

- [ ] **Step 3: Commit and open a PR to confirm CI runs green**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add build, format, and test workflow"
git push -u origin HEAD
gh pr create --fill
```

Expected: the CI check passes on the PR.

---

## Deployment (manual, once)

Not a coding task, but required before Milestone 2. Do it now while the surface is small.

- [ ] Create a Neon project; copy the **pooled** connection string.
- [ ] Create a Render Web Service from the repo. Build: `dotnet publish src/ProgressiveOverload.Api -c Release -o out`. Start: `dotnet out/ProgressiveOverload.Api.dll`. **Use a paid instance type** — the free tier sleeps, which will break the week rollover job in Milestone 4 (spec §9).
- [ ] Set environment variables on Render: `ConnectionStrings__Postgres`, `Jwt__SigningKey` (32+ random bytes), `GoogleAuth__ClientId`, `Sentry__Dsn`.
- [ ] Set `Sentry__Release` to the deployed commit SHA. On Render, use the build command to inject it: `Sentry__Release=$RENDER_GIT_COMMIT dotnet publish ...`, or set it explicitly in the deploy step. Without this, every Sentry error is attributed to an unknown release and you cannot tell which deploy introduced a regression (spec §11).
- [ ] Attach the custom domain `api.progressiveoverload.app`. This is required, not cosmetic — the `SameSite=Strict` refresh cookie only works if the API is same-site with the web app (spec §7).
- [ ] Add a migration step to the deploy: `dotnet ef database update` must run **before** the new revision serves traffic.
- [ ] Verify `GET https://api.progressiveoverload.app/health` returns `{"status":"ok"}`.
- [ ] Register a real account against production and confirm the Sentry project receives a test event.

---

## Milestone 1 Definition of Done

- [ ] `dotnet test` passes: ~18 domain tests, ~24 integration tests.
- [ ] A real person can register, log in, sign in with Google, stay signed in across a browser restart, and edit their profile against the deployed API.
- [ ] Replaying a redeemed refresh token logs the session out — verified manually against production.
- [ ] `curl` against `/api/v1/me` without a token returns 401; with another user's token, it returns that user's data and never anyone else's.
- [ ] No secret appears anywhere in git history.

**Next:** Milestone 1b (web client auth shell) or Milestone 2 (exercise catalog and workout logging). Milestone 2's plan should be written after this one ships, so it can use what you learned here.
