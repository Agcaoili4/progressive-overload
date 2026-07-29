namespace ProgressiveOverload.Application.Abstractions;

/*
    Injected everywhere time is read. Calling DateTimeOffset.UtcNow directly in a handler
    makes the behaviour untestable, and this codebase's most important logic (week
    boundaries, token expiry) is time-dependent.
*/
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
