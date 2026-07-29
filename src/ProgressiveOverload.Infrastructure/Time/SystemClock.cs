using ProgressiveOverload.Application.Abstractions;

namespace ProgressiveOverload.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
