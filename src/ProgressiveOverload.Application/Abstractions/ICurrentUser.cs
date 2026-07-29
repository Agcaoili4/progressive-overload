namespace ProgressiveOverload.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
