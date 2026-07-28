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
