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
