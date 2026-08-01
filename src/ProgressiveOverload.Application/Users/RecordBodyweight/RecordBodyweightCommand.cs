namespace ProgressiveOverload.Application.Users.RecordBodyweight;

public sealed record RecordBodyweightCommand(decimal WeightKg, DateTimeOffset? RecordedAt);

public sealed record BodyweightResponse(Guid Id, decimal WeightKg, DateTimeOffset RecordedAt);
